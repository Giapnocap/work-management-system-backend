using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkManagementSystem.API.Configuration;
using WorkManagementSystem.API.Controllers;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Infrastructure.Health;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class SecurityOperationsTests
{
    [Fact]
    public void JwtOptions_RejectsPlaceholderKeyInProduction()
    {
        var options = new JwtOptions
        {
            Key = "CHANGE_ME_LOCAL_DEVELOPMENT_SECRET_KEY_32_CHARS_MINIMUM",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 180
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isProduction: true));
    }

    [Fact]
    public void JwtOptions_RejectsExampleReplacementKeyInProduction()
    {
        var options = new JwtOptions
        {
            Key = "ReplaceWithARandomJwtSigningKeyAtLeast32CharactersLong",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 60
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isProduction: true));
    }

    [Fact]
    public void JwtOptions_RejectsLongLivedAccessTokenInProduction()
    {
        var options = new JwtOptions
        {
            Key = "A_STRONG_PRODUCTION_SIGNING_KEY_32_CHARACTERS_MINIMUM",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 180
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isProduction: true));
        options.Validate(isProduction: false);
    }

    [Fact]
    public void StartupConfiguration_GeneratesEphemeralJwtKeyOnlyInDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();

        var first = StartupConfigurationValidator.GetJwtOptions(
            configuration,
            isDevelopment: true,
            isProduction: false);
        var second = StartupConfigurationValidator.GetJwtOptions(
            configuration,
            isDevelopment: true,
            isProduction: false);

        Assert.True(first.Key.Length >= 32);
        Assert.NotEqual(first.Key, second.Key);
        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetJwtOptions(
                configuration,
                isDevelopment: false,
                isProduction: true));
    }

    [Fact]
    public void StartupConfiguration_RejectsUnencryptedProductionDatabase()
    {
        var configuration = CreateStartupConfiguration(
            "Server=localhost;Database=WorkManagementDB;Encrypt=False;TrustServerCertificate=True;");

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetConnectionString(configuration, isProduction: true));
    }

    [Fact]
    public void StartupConfiguration_RejectsWildcardProductionHosts()
    {
        var configuration = CreateStartupConfiguration(
            "Server=localhost;Database=WorkManagementDB;Encrypt=True;TrustServerCertificate=False;",
            allowedHosts: "*");

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetConnectionString(configuration, isProduction: true));
    }

    [Fact]
    public void StartupConfiguration_RejectsDemoSeedInProduction()
    {
        var configuration = CreateStartupConfiguration(
            "Server=localhost;Database=WorkManagementDB;Encrypt=True;TrustServerCertificate=False;",
            demoSeedEnabled: true);

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetConnectionString(configuration, isProduction: true));
    }

    [Fact]
    public void StartupConfiguration_RejectsHttpCorsOriginInProduction()
    {
        var configuration = CreateStartupConfiguration(
            "Server=localhost;Database=WorkManagementDB;Encrypt=True;TrustServerCertificate=False;",
            corsOrigin: "http://frontend.example.com");

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetCorsOrigins(configuration, isProduction: true));
    }

    [Theory]
    [InlineData("https://frontend.example.com/app")]
    [InlineData("https://frontend.example.com?source=test")]
    [InlineData("https://user@frontend.example.com")]
    public void StartupConfiguration_RejectsCorsValuesThatAreNotOrigins(string value)
    {
        var configuration = CreateStartupConfiguration(
            "Server=localhost;Database=WorkManagementDB;Encrypt=True;TrustServerCertificate=False;",
            corsOrigin: value);

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetCorsOrigins(configuration, isProduction: false));
    }

    [Fact]
    public void StartupConfiguration_NormalizesAndDeduplicatesCorsOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = " https://frontend.example.com/ ",
                ["Cors:AllowedOrigins:1"] = "https://FRONTEND.example.com"
            })
            .Build();

        var origins = StartupConfigurationValidator.GetCorsOrigins(
            configuration,
            isProduction: true);

        Assert.Equal(new[] { "https://frontend.example.com" }, origins);
    }

    [Fact]
    public void StartupConfiguration_AcceptsRestrictedSecureProductionSettings()
    {
        const string connectionString =
            "Server=localhost;Database=WorkManagementDB;Encrypt=True;TrustServerCertificate=False;";
        var configuration = CreateStartupConfiguration(connectionString);

        var validatedConnection = StartupConfigurationValidator.GetConnectionString(
            configuration,
            isProduction: true);
        var origins = StartupConfigurationValidator.GetCorsOrigins(
            configuration,
            isProduction: true);

        Assert.Equal(connectionString, validatedConnection);
        Assert.Equal(new[] { "https://frontend.example.com" }, origins);
    }

    [Fact]
    public void ReverseProxyConfiguration_RequiresTrustedSourceInProduction()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Enabled"] = "true",
                ["ReverseProxy:ForwardLimit"] = "1"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.GetReverseProxySettings(
                configuration,
                isProduction: true));
    }

    [Fact]
    public void ReverseProxyConfiguration_ParsesTrustedProxyAndNetwork()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Enabled"] = "true",
                ["ReverseProxy:ForwardLimit"] = "2",
                ["ReverseProxy:KnownProxies:0"] = "10.0.0.10",
                ["ReverseProxy:KnownNetworks:0"] = "10.10.0.0/16"
            })
            .Build();

        var settings = StartupConfigurationValidator.GetReverseProxySettings(
            configuration,
            isProduction: true);

        Assert.True(settings.Enabled);
        Assert.Equal(2, settings.ForwardLimit);
        Assert.Single(settings.ParseKnownProxies());
        Assert.Single(settings.ParseKnownNetworks());
    }

    [Fact]
    public void LoginAndRegister_HaveAuthenticationRateLimitPolicy()
    {
        AssertRateLimitPolicy(nameof(AuthController.Login));
        AssertRateLimitPolicy(nameof(AuthController.Register));
    }

    [Fact]
    public void Upload_HasUploadRateLimitPolicy()
    {
        var method = typeof(UploadController).GetMethod(nameof(UploadController.Upload))
            ?? throw new InvalidOperationException("Upload method not found.");
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("uploads", attribute.PolicyName);
    }

    [Fact]
    public async Task SecurityHeadersMiddleware_AddsHeadersAndDisablesApiCaching()
    {
        var middleware = new SecurityHeadersMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.StartAsync();
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/tasks";

        await middleware.Invoke(context);

        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WithAvailableDatabase_ReturnsHealthy()
    {
        await using var context = TestFactory.CreateDbContext();
        var healthCheck = new DatabaseHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static void AssertRateLimitPolicy(string methodName)
    {
        var method = typeof(AuthController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method {methodName} not found.");
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("authentication", attribute.PolicyName);
    }

    private static IConfiguration CreateStartupConfiguration(
        string connectionString,
        string allowedHosts = "api.example.com",
        bool demoSeedEnabled = false,
        string corsOrigin = "https://frontend.example.com")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["AllowedHosts"] = allowedHosts,
                ["DemoSeed:Enabled"] = demoSeedEnabled.ToString(),
                ["Cors:AllowedOrigins:0"] = corsOrigin
            })
            .Build();
    }
}
