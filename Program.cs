using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using System.Reflection;
using WorkManagementSystem.API.Configuration;
using WorkManagementSystem.API.Contracts;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DependencyInjection;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.DependencyInjection;
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
var allowedCorsOrigins = StartupConfigurationValidator.GetCorsOrigins(
    builder.Configuration,
    builder.Environment.IsProduction());

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(connectionString, uploadCleanupOptions)
    .AddApiPresentation(builder.Environment.IsDevelopment(), allowedCorsOrigins)
    .AddApiSecurity(jwtOptions, reverseProxySettings)
    .AddApiHealthChecks()
    .AddApiDocumentation();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = UploadFileValidator.MaxFileSizeBytes;
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
