using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;
using WorkManagementSystem.Domain.Entities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.API.Swagger;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.API.Authentication;
using WorkManagementSystem.API.Contracts;
using WorkManagementSystem.API.Configuration;
using WorkManagementSystem.Infrastructure.Health;
using WorkManagementSystem.Infrastructure.Security;
using WorkManagementSystem.Infrastructure.Storage;

// ================= SERILOG =================
const string logOutputTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{UserId}] {Message:lj}{NewLine}{Exception}";

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddUserSecrets(
        Assembly.GetExecutingAssembly(),
        optional: true,
        reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    var logFilePath = Path.Combine(
        context.HostingEnvironment.ContentRootPath,
        "logs",
        "log-.txt");
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "WorkManagementSystem")
        .WriteTo.Console(outputTemplate: logOutputTemplate)
        .WriteTo.File(
            logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: logOutputTemplate);
});

var jwtOptions = StartupConfigurationValidator.GetJwtOptions(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    builder.Environment.IsProduction());
builder.Services.AddSingleton(Options.Create(jwtOptions));

var connectionString = StartupConfigurationValidator.GetConnectionString(
    builder.Configuration,
    builder.Environment.IsProduction());
var reverseProxySettings = StartupConfigurationValidator.GetReverseProxySettings(
    builder.Configuration,
    builder.Environment.IsProduction());
var uploadCleanupOptions = builder.Configuration
    .GetSection(UploadCleanupOptions.SectionName)
    .Get<UploadCleanupOptions>() ?? new UploadCleanupOptions();
uploadCleanupOptions.Validate();
builder.Services.AddSingleton(Options.Create(uploadCleanupOptions));

// ================= SERVICES =================
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ApiProblemDetailsFactory.CreateValidationResponse;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 8 * 1024;
    options.MaximumParallelInvocationsPerClient = 1;
});
builder.Services.AddSingleton<ITaskRealtimeNotifier, SignalRTaskRealtimeNotifier>();

// DB
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Historical KPI and employment rows remain visible after their user is soft-deleted.
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});
builder.Services.AddScoped<IAppDbContext>(provider =>
    provider.GetRequiredService<AppDbContext>());

// Repository
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<ITransactionManager, EfTransactionManager>();

// Services
builder.Services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
builder.Services.AddSingleton<IPasswordHashService, BcryptPasswordHashService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ITaskQueryService, TaskQueryService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IProgressQueryService, ProgressQueryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IKpiPeriodResolver, KpiPeriodResolver>();
builder.Services.AddScoped<IUserPerformanceService, UserPerformanceService>();
builder.Services.AddScoped<IUserWorkHistoryService, UserWorkHistoryService>();
builder.Services.AddScoped<IUserTaskAssignmentService, UserTaskAssignmentService>();
builder.Services.AddScoped<IUserUnitMembershipService, UserUnitMembershipService>();
builder.Services.AddScoped<IStaffMovementService, StaffMovementService>();
builder.Services.AddSingleton<IUploadFileValidator, UploadFileValidator>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<UploadOrphanCleaner>();
if (uploadCleanupOptions.Enabled)
    builder.Services.AddHostedService<UploadOrphanCleanupWorker>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IChangePasswordService, ChangePasswordService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ISubTaskService, SubTaskService>();
builder.Services.AddScoped<ITaskAccessService, TaskAccessService>();
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
builder.Services.AddScoped<ITaskBusinessRuleService, TaskBusinessRuleService>();
builder.Services.AddScoped<ITaskDtoBuilder, TaskDtoBuilder>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IKpiService, KpiService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// AutoMapper
builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

// ================= LIMITS =================
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadFileValidator.MaxFileSizeBytes;
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = UploadFileValidator.MaxFileSizeBytes;
});

// ================= CORS =================
var allowedCorsOrigins = StartupConfigurationValidator.GetCorsOrigins(
    builder.Configuration,
    builder.Environment.IsProduction());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ================= AUTH (JWT) =================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
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
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/discussionHub"))
                {
                    context.Token = accessToken;
                }
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

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
    options.AddPolicy("uploads", context =>
    {
        var userId = context.User.FindFirst(AuthenticationClaimTypes.UserId)?.Value;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? remoteIp
            : $"{userId}:{remoteIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
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
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
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

builder.Services.AddHealthChecks()
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

// ================= SWAGGER =================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WorkManagement API",
        Version = "v1",
        Description = "Department-based work management API with JWT authentication, task progress review, uploads, and KPI periods."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token dạng: Bearer {token}"
    });
    c.OperationFilter<AuthorizeOperationFilter>();
    c.OperationFilter<DefaultResponseOperationFilter>();
    c.DocumentFilter<ApiTagsDocumentFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// ================= BUILD APP =================
var app = builder.Build();

if (app.Configuration.GetValue<bool>("DemoSeed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    await DemoDataSeeder.SeedAsync(
        scope.ServiceProvider,
        app.Logger,
        app.Lifetime.ApplicationStopping);
}

// ================= MIDDLEWARE =================
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

if (reverseProxySettings.Enabled)
    app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

        var userId = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(AuthenticationClaimTypes.UserId)?.Value
            : null;
        if (!string.IsNullOrWhiteSpace(userId))
            diagnosticContext.Set("UserId", userId);
    };
});
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages(async statusContext =>
{
    var httpContext = statusContext.HttpContext;
    var response = httpContext.Response;

    if (response.StatusCode == StatusCodes.Status404NotFound)
    {
        await ApiProblemDetailsFactory.WriteAsync(
            httpContext,
            StatusCodes.Status404NotFound,
            "not_found",
            "Khong tim thay tai nguyen yeu cau.",
            cancellationToken: httpContext.RequestAborted);
    }
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseMiddleware<UserLogContextMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DiscussionHub>("/discussionHub");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

public partial class Program
{
}
