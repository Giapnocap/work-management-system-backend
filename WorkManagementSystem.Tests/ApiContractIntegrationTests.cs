using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class ApiContractIntegrationTests
{
    [Fact]
    public async Task InvalidModel_ReturnsValidationProblemWithFieldErrors()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/login", new LoginDto());

        var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "validation_error");
        var errors = problem.GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), property =>
            property.Name.Equals(nameof(LoginDto.Username), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors.EnumerateObject(), property =>
            property.Name.Equals(nameof(LoginDto.Password), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorizedProblem()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var response = await app.Client.GetAsync("/api/profile");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task RoleRestrictedEndpoint_ReturnsForbiddenProblem()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("employee-it", "Password@123"));

        var response = await app.Client.GetAsync("/api/audit-logs");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task TokenVersionChange_InvalidatesPreviouslyIssuedToken()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("employee-it", "Password@123"));
        (await app.Client.GetAsync("/api/profile")).EnsureSuccessStatusCode();

        await app.InvalidateSessionsAsync(app.EmployeeId);

        var response = await app.Client.GetAsync("/api/profile");
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task ExpiredJwt_ReturnsUnauthorizedProblem()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(app.CreateExpiredEmployeeToken());

        var response = await app.Client.GetAsync("/api/profile");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundProblem()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var response = await app.Client.GetAsync("/api/route-that-does-not-exist");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task MissingDomainResource_ReturnsNotFoundProblem()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));

        var response = await app.Client.GetAsync($"/api/progress/task/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task ResourceCreationAndDeletion_UseCreatedAndNoContentStatuses()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));

        var projectResponse = await app.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = $"API contract {Guid.NewGuid():N}",
            UnitId = app.UnitId
        });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        var taskResponse = await app.Client.PostAsJsonAsync("/api/tasks", new CreateTaskDto
        {
            Title = "API contract task",
            ProjectId = project.Id,
            UserIds = new List<Guid> { app.EmployeeId }
        });
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        Assert.NotNull(taskResponse.Headers.Location);
        var task = await taskResponse.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(task);

        var deleteTaskResponse = await app.Client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteTaskResponse.StatusCode);
        Assert.Equal(0, deleteTaskResponse.Content.Headers.ContentLength ?? 0);

        var archiveProjectResponse = await app.Client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveProjectResponse.StatusCode);
    }

    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var problem = document.RootElement;
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal(JsonValueKind.Object, problem.GetProperty("errors").ValueKind);
        return problem.Clone();
    }
}
