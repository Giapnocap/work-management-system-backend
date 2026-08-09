using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Infrastructure.Health;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class OperationalObservabilityTests
{
    [Fact]
    public async Task UserLogContextMiddleware_AddsAuthenticatedUserIdToStructuredLogs()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AuthenticationClaimTypes.UserId, userId.ToString())
            }, "Test"))
        };
        var middleware = new UserLogContextMiddleware(_ =>
        {
            logger.Information("Request reached the test endpoint.");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var logEvent = Assert.Single(sink.Events);
        var property = Assert.IsType<ScalarValue>(logEvent.Properties["UserId"]);
        Assert.Equal(userId.ToString(), property.Value);
    }

    [Fact]
    public async Task UserLogContextMiddleware_DoesNotTrustUnauthenticatedClaims()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AuthenticationClaimTypes.UserId, Guid.NewGuid().ToString())
            }))
        };
        var middleware = new UserLogContextMiddleware(_ =>
        {
            logger.Information("Anonymous request reached the test endpoint.");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var logEvent = Assert.Single(sink.Events);
        Assert.DoesNotContain("UserId", logEvent.Properties.Keys);
    }

    [Fact]
    public async Task UploadStorageHealthCheck_WithWritableDirectory_ReturnsHealthyAndRemovesProbe()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var uploadsRoot = Directory.CreateDirectory(Path.Combine(root, "Uploads")).FullName;
            var healthCheck = new UploadStorageHealthCheck(new TestWebHostEnvironment(root));

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Empty(Directory.EnumerateFiles(uploadsRoot, ".health-*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UploadStorageHealthCheck_WithoutDirectory_ReturnsUnhealthy()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var healthCheck = new UploadStorageHealthCheck(new TestWebHostEnvironment(root));

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymousAndSeparateLiveFromReadyDependencies()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var liveResponse = await app.Client.GetAsync("/health/live");
        var readyResponse = await app.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        var registrations = app.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;
        Assert.Contains(registrations, registration =>
            registration.Name == "self" && registration.Tags.Contains("live"));
        Assert.Contains(registrations, registration =>
            registration.Name == "database" && registration.Tags.Contains("ready") &&
            !registration.Tags.Contains("live") &&
            registration.Timeout == TimeSpan.FromSeconds(5));
        Assert.Contains(registrations, registration =>
            registration.Name == "upload-storage" && registration.Tags.Contains("ready") &&
            !registration.Tags.Contains("live") &&
            registration.Timeout == TimeSpan.FromSeconds(5));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "WorkManagementSystem.HealthTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string ApplicationName { get; set; } = "WorkManagementSystem.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "IntegrationTest";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
