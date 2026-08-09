using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Tests.TestSupport;

internal sealed class IntegrationTestApp : IAsyncDisposable
{
    private const string JwtKey = "INTEGRATION_TEST_SECRET_KEY_123456789_ABCDEF_32";
    private const string JwtIssuer = "WorkManagementSystem.IntegrationTests";
    private const string JwtAudience = "WorkManagementSystem.IntegrationTests.Client";

    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly string _contentRoot;

    private IntegrationTestApp(
        IntegrationTestWebApplicationFactory factory,
        HttpClient client,
        string contentRoot,
        Guid unitId,
        Guid adminId,
        Guid managerId,
        Guid employeeId)
    {
        _factory = factory;
        Client = client;
        _contentRoot = contentRoot;
        UnitId = unitId;
        AdminId = adminId;
        ManagerId = managerId;
        EmployeeId = employeeId;
    }

    public HttpClient Client { get; }
    public IServiceProvider Services => _factory.Services;
    public Guid UnitId { get; }
    public Guid AdminId { get; }
    public Guid ManagerId { get; }
    public Guid EmployeeId { get; }

    public static async Task<IntegrationTestApp> CreateAsync()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            "WorkManagementSystem.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        await WriteConfigurationAsync(contentRoot);

        var factory = new IntegrationTestWebApplicationFactory(
            contentRoot,
            $"IntegrationTest-{Guid.NewGuid():N}");

        try
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            var seed = await SeedAsync(factory.Services);

            return new IntegrationTestApp(
                factory,
                client,
                contentRoot,
                seed.UnitId,
                seed.AdminId,
                seed.ManagerId,
                seed.EmployeeId);
        }
        catch
        {
            await factory.DisposeAsync();
            TryDeleteDirectory(contentRoot);
            throw;
        }
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

    public string CreateExpiredEmployeeToken()
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(AuthenticationClaimTypes.UserId, EmployeeId.ToString()),
                new Claim(AuthenticationClaimTypes.TokenVersion, "0"),
                new Claim(ClaimTypes.Name, "employee-it"),
                new Claim(ClaimTypes.Role, SystemRoles.User)
            }),
            NotBefore = now.AddMinutes(-10),
            Expires = now.AddMinutes(-5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    public void Authorize(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InvalidateSessionsAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        user.InvalidateSessions();
        await context.SaveChangesAsync();
    }

    public async Task<T> PostJsonAsync<T>(string url, object body)
    {
        var response = await Client.PostAsJsonAsync(url, body);
        await response.AssertSuccessAsync();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException($"No JSON response from {url}.");
    }

    public async Task<T> GetJsonAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        await response.AssertSuccessAsync();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException($"No JSON response from {url}.");
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
        await _factory.DisposeAsync();
        TryDeleteDirectory(_contentRoot);
    }

    private static async Task<(Guid UnitId, Guid AdminId, Guid ManagerId, Guid EmployeeId)> SeedAsync(
        IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();

        var now = DateTime.UtcNow;
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = "Integration Unit"
        };
        var passwordHash = passwordHashService.Hash("Password@123");

        var admin = CreateUser("admin-it", SystemRoles.Admin, "Integration Admin", "ADM9999", null, now, passwordHash);
        var manager = CreateUser("manager-it", SystemRoles.Manager, "Integration Manager", "MGR9999", unit.Id, now, passwordHash);
        var employee = CreateUser("employee-it", SystemRoles.User, "Integration Employee", "EMP9999", unit.Id, now, passwordHash);

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
                Role = SystemRoles.Manager,
                EffectiveFrom = now.AddDays(-1),
                ChangeReason = "Integration seed",
                CreatedAt = now
            },
            new UserWorkHistory
            {
                Id = Guid.NewGuid(),
                UserId = employee.Id,
                UnitId = unit.Id,
                Role = SystemRoles.User,
                EffectiveFrom = now.AddDays(-1),
                ChangeReason = "Integration seed",
                CreatedAt = now
            });

        await context.SaveChangesAsync();
        return (unit.Id, admin.Id, manager.Id, employee.Id);
    }

    private static User CreateUser(
        string username,
        string role,
        string fullName,
        string employeeCode,
        Guid? unitId,
        DateTime joinedAt,
        string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = fullName,
            EmployeeCode = employeeCode,
            PasswordHash = passwordHash,
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = joinedAt,
            IsApproved = true
        };
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Temporary test files are removed on a best-effort basis.
        }
    }

    private static Task WriteConfigurationAsync(string contentRoot)
    {
        var configuration = new
        {
            ConnectionStrings = new
            {
                Default = "Server=localhost;Database=IntegrationTests;Integrated Security=True;TrustServerCertificate=True"
            },
            Jwt = new
            {
                Key = JwtKey,
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                ExpirationMinutes = 180
            },
            Cors = new
            {
                AllowedOrigins = new[] { "https://localhost" }
            },
            ReverseProxy = new
            {
                Enabled = false,
                ForwardLimit = 1,
                KnownProxies = Array.Empty<string>(),
                KnownNetworks = Array.Empty<string>()
            },
            UploadCleanup = new
            {
                Enabled = false,
                MinimumAgeHours = 24,
                IntervalHours = 24
            },
            DemoSeed = new
            {
                Enabled = false,
                ApplyMigrations = false
            },
            AllowedHosts = "localhost"
        };

        return File.WriteAllTextAsync(
            Path.Combine(contentRoot, "appsettings.json"),
            JsonSerializer.Serialize(configuration));
    }

    private sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _contentRoot;
        private readonly string _databaseName;

        public IntegrationTestWebApplicationFactory(string contentRoot, string databaseName)
        {
            _contentRoot = contentRoot;
            _databaseName = databaseName;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTest");
            builder.UseContentRoot(_contentRoot);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                    options.ConfigureWarnings(warnings =>
                        warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
                });
            });
        }
    }
}

internal static class HttpResponseMessageAssertions
{
    public static async Task AssertSuccessAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException(
            $"Expected success status code, got {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }
}
