using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using WorkManagementSystem.API.Authentication;
using WorkManagementSystem.API.Contracts;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.API.Swagger;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Infrastructure.Health;

namespace WorkManagementSystem.API.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiPresentation(
        this IServiceCollection services,
        bool isDevelopment,
        string[] allowedCorsOrigins)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = ApiProblemDetailsFactory.CreateValidationResponse;
        });
        services.AddEndpointsApiExplorer();
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = isDevelopment;
            options.MaximumReceiveMessageSize = 8 * 1024;
            options.MaximumParallelInvocationsPerClient = 1;
        });
        services.AddSingleton<ITaskRealtimeNotifier, SignalRTaskRealtimeNotifier>();
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = UploadFileValidator.MaxFileSizeBytes;
        });
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedCorsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddApiSecurity(
        this IServiceCollection services,
        JwtOptions jwtOptions,
        ReverseProxySettings reverseProxySettings)
    {
        services.AddSingleton(Options.Create(jwtOptions));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ClockSkew = TimeSpan.FromMinutes(1),
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/discussionHub"))
                            context.Token = accessToken;

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = JwtSessionValidator.ValidateAsync,
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        if (!context.Response.HasStarted)
                        {
                            await ApiProblemDetailsFactory.WriteAsync(
                                context.HttpContext,
                                StatusCodes.Status401Unauthorized,
                                "unauthorized",
                                "Yeu cau can dang nhap hoac token khong hop le.",
                                cancellationToken: context.HttpContext.RequestAborted);
                        }
                    },
                    OnForbidden = async context =>
                    {
                        if (!context.Response.HasStarted)
                        {
                            await ApiProblemDetailsFactory.WriteAsync(
                                context.HttpContext,
                                StatusCodes.Status403Forbidden,
                                "forbidden",
                                "Ban khong co quyen thuc hien thao tac nay.",
                                cancellationToken: context.HttpContext.RequestAborted);
                        }
                    }
                };
            });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => CreateFixedWindowOptions(8, TimeSpan.FromMinutes(1))));
            options.AddPolicy("uploads", context =>
            {
                var userId = context.User.FindFirst(AuthenticationClaimTypes.UserId)?.Value;
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var partitionKey = string.IsNullOrWhiteSpace(userId)
                    ? remoteIp
                    : $"{userId}:{remoteIp}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => CreateFixedWindowOptions(20, TimeSpan.FromMinutes(10)));
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                await ApiProblemDetailsFactory.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "rate_limit_exceeded",
                    "Qua nhieu yeu cau. Vui long thu lai sau.",
                    cancellationToken: cancellationToken);
            };
        });

        if (reverseProxySettings.Enabled)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = reverseProxySettings.ForwardLimit;

                var knownProxies = reverseProxySettings.ParseKnownProxies();
                var knownNetworks = reverseProxySettings.ParseKnownNetworks();
                if (knownProxies.Count > 0 || knownNetworks.Count > 0)
                {
                    options.KnownProxies.Clear();
                    options.KnownNetworks.Clear();
                    foreach (var address in knownProxies)
                        options.KnownProxies.Add(address);
                    foreach (var network in knownNetworks)
                        options.KnownNetworks.Add(network);
                }
            });
        }

        return services;
    }

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddCheck<DatabaseHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<UploadStorageHealthCheck>(
                "upload-storage",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WorkManagement API",
                Version = "v1",
                Description = "Department-based work management API with JWT authentication, task progress review, uploads, and KPI periods."
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhap token dang: Bearer {token}"
            });
            options.OperationFilter<AuthorizeOperationFilter>();
            options.OperationFilter<DefaultResponseOperationFilter>();
            options.DocumentFilter<ApiTagsDocumentFilter>();

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(
        int permitLimit,
        TimeSpan window)
    {
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };
    }
}
