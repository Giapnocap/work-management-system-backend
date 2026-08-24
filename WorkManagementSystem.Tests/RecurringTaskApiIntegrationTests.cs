using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class RecurringTaskApiIntegrationTests
{
    [Fact]
    public async Task Swagger_ExposesRecurringTaskManagementContract()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var swagger = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Contains("/api/recurring-tasks", swagger.Paths.Keys);
        Assert.Contains("/api/recurring-tasks/{id}", swagger.Paths.Keys);
        Assert.Contains("/api/recurring-tasks/{id}/pause", swagger.Paths.Keys);
        Assert.Contains("/api/recurring-tasks/{id}/resume", swagger.Paths.Keys);
        Assert.Contains(OperationType.Post, swagger.Paths["/api/recurring-tasks"].Operations.Keys);
        Assert.Contains(OperationType.Delete, swagger.Paths["/api/recurring-tasks/{id}"].Operations.Keys);
    }

    [Fact]
    public async Task Manager_CanCreateReadPauseResumeAndDeleteRecurringTask()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));

        var createResponse = await app.Client.PostAsJsonAsync(
            "/api/recurring-tasks",
            new CreateRecurringTaskDto
            {
                Title = "Daily integration task",
                Description = "Recurring API workflow",
                Priority = "Medium",
                RecurrenceType = "Daily",
                Interval = 1,
                NextRunAtUtc = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
                UserIds = new List<Guid> { app.EmployeeId }
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<RecurringTaskTemplateDto>();
        Assert.NotNull(created);
        Assert.Equal($"/api/recurring-tasks/{created.Id}", createResponse.Headers.Location?.AbsolutePath);
        Assert.Equal(app.UnitId, created.UnitId);
        Assert.Equal(app.EmployeeId, Assert.Single(created.Assignees).Id);

        var loaded = await app.GetJsonAsync<RecurringTaskTemplateDto>(
            $"/api/recurring-tasks/{created.Id}");
        Assert.Equal(created.Id, loaded.Id);

        var pauseResponse = await app.Client.PostAsync(
            $"/api/recurring-tasks/{created.Id}/pause",
            content: null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);
        Assert.False((await pauseResponse.Content.ReadFromJsonAsync<RecurringTaskTemplateDto>())!.IsActive);

        var resumeResponse = await app.Client.PostAsync(
            $"/api/recurring-tasks/{created.Id}/resume",
            content: null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        Assert.True((await resumeResponse.Content.ReadFromJsonAsync<RecurringTaskTemplateDto>())!.IsActive);

        var deleteResponse = await app.Client.DeleteAsync($"/api/recurring-tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await app.Client.GetAsync($"/api/recurring-tasks/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Employee_CannotAccessRecurringTaskManagement()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("employee-it", "Password@123"));

        var response = await app.Client.GetAsync("/api/recurring-tasks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
