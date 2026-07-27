using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkManagementSystem.Application.Common;
using Microsoft.IdentityModel.Tokens;
using WorkManagementSystem.API.Controllers;
using WorkManagementSystem.API.Authentication;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Tests.TestSupport;

internal sealed class IntegrationTestApp : IAsyncDisposable
{
    private const string JwtKey = "INTEGRATION_TEST_SECRET_KEY_123456789_ABCDEF_32";

    private readonly WebApplication _app;
    private readonly string _contentRoot;

    private IntegrationTestApp(WebApplication app, HttpClient client, string contentRoot, Guid unitId, Guid adminId, Guid managerId, Guid employeeId)
    {
        _app = app;
        Client = client;
        _contentRoot = contentRoot;
        UnitId = unitId;
        AdminId = adminId;
        ManagerId = managerId;
        EmployeeId = employeeId;
    }

    public HttpClient Client { get; }
    public Guid UnitId { get; }
    public Guid AdminId { get; }
    public Guid ManagerId { get; }
    public Guid EmployeeId { get; }

    public static async Task<IntegrationTestApp> CreateAsync()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "WorkManagementSystem.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var port = GetFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "IntegrationTest",
            ContentRootPath = contentRoot
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = "WorkManagementSystem.IntegrationTests",
            ["Jwt:Audience"] = "WorkManagementSystem.IntegrationTests.Client",
            ["Jwt:ExpirationMinutes"] = "180",
            ["ConnectionStrings:Default"] = $"IntegrationTest-{Guid.NewGuid():N}"
        });

        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<DiscussionHub>("/discussionHub");

        var seed = await SeedAsync(app.Services);
        await app.StartAsync();

        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}")
        };

        return new IntegrationTestApp(app, client, contentRoot, seed.UnitId, seed.AdminId, seed.ManagerId, seed.EmployeeId);
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = username,
            Password = password
        });

        await response.AssertSuccessAsync();
        var token = (await response.Content.ReadAsStringAsync()).Trim();
        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
            token = token[1..^1];

        return string.IsNullOrWhiteSpace(token)
            ? throw new InvalidOperationException("Login did not return a token.")
            : token;
    }

    public void Authorize(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T> PostJsonAsync<T>(string url, object body)
    {
        var response = await Client.PostAsJsonAsync(url, body);
        await response.AssertSuccessAsync();
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException($"No JSON response from {url}.");
    }

    public async Task<T> GetJsonAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        await response.AssertSuccessAsync();
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException($"No JSON response from {url}.");
    }

    public async Task<UploadFileDto> UploadTextFileAsync(Guid taskId)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Completed work proof."));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "proof.txt");

        var response = await Client.PostAsync($"/api/Upload?taskId={taskId}", content);
        await response.AssertSuccessAsync();
        return await response.Content.ReadFromJsonAsync<UploadFileDto>()
            ?? throw new InvalidOperationException("Upload did not return file metadata.");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();

        try
        {
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, recursive: true);
        }
        catch
        {
            // Test temp cleanup is best-effort.
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
            .AddApplicationPart(typeof(AuthController).Assembly);
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "Gia tri khong hop le."
                                : error.ErrorMessage).ToArray());

                return new BadRequestObjectResult(new
                {
                    message = "Du lieu gui len khong hop le.",
                    code = "validation_error",
                    traceId = context.HttpContext.TraceIdentifier,
                    details = "",
                    errors
                });
            };
        });
        services.AddSignalR();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(configuration.GetConnectionString("Default")!);
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITransactionManager, EfTransactionManager>();

        services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IKpiPeriodResolver, KpiPeriodResolver>();
        services.AddScoped<IUserPerformanceService, UserPerformanceService>();
        services.AddScoped<IUserWorkHistoryService, UserWorkHistoryService>();
        services.AddScoped<IUserTaskAssignmentService, UserTaskAssignmentService>();
        services.AddScoped<IUserUnitMembershipService, UserUnitMembershipService>();
        services.AddScoped<IStaffMovementService, StaffMovementService>();
        services.AddSingleton<IUploadFileValidator, UploadFileValidator>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IChangePasswordService, ChangePasswordService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ISubTaskService, SubTaskService>();
        services.AddScoped<ITaskAccessService, TaskAccessService>();
        services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
        services.AddScoped<ITaskBusinessRuleService, TaskBusinessRuleService>();
        services.AddScoped<ITaskDtoBuilder, TaskDtoBuilder>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<IAuditService, AuditService>();

        services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = UploadFileValidator.MaxFileSizeBytes;
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = JwtSessionValidator.ValidateAsync
                };
            });

        services.AddAuthorization();
    }

    private static async Task<(Guid UnitId, Guid AdminId, Guid ManagerId, Guid EmployeeId)> SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = "Integration Unit"
        };

        var admin = CreateUser("admin-it", "Admin", "Integration Admin", "ADM9999", null, now);
        var manager = CreateUser("manager-it", "Manager", "Integration Manager", "MGR9999", unit.Id, now);
        var employee = CreateUser("employee-it", "User", "Integration Employee", "EMP9999", unit.Id, now);

        context.Units.Add(unit);
        context.Users.AddRange(admin, manager, employee);
        context.UserUnits.AddRange(
            new UserUnit { Id = Guid.NewGuid(), UserId = manager.Id, UnitId = unit.Id },
            new UserUnit { Id = Guid.NewGuid(), UserId = employee.Id, UnitId = unit.Id });
        context.UserWorkHistories.AddRange(
            new UserWorkHistory
            {
                Id = Guid.NewGuid(),
                UserId = manager.Id,
                UnitId = unit.Id,
                Role = "Manager",
                EffectiveFrom = now.AddDays(-1),
                ChangeReason = "Integration seed",
                CreatedAt = now
            },
            new UserWorkHistory
            {
                Id = Guid.NewGuid(),
                UserId = employee.Id,
                UnitId = unit.Id,
                Role = "User",
                EffectiveFrom = now.AddDays(-1),
                ChangeReason = "Integration seed",
                CreatedAt = now
            });

        await context.SaveChangesAsync();
        return (unit.Id, admin.Id, manager.Id, employee.Id);
    }

    private static User CreateUser(string username, string role, string fullName, string employeeCode, Guid? unitId, DateTime joinedAt)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = fullName,
            EmployeeCode = employeeCode,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = joinedAt,
            IsApproved = true
        };
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal static class HttpResponseMessageAssertions
{
    public static async Task AssertSuccessAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException($"Expected success status code, got {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }
}
