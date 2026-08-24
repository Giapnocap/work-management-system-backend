using System.Net;
using System.Net.Http.Json;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class TaskTimelineApiIntegrationTests
{
    [Fact]
    public async Task TimelineEndpoint_ReturnsAuthorizedTaskActivityAndBlocksOtherUnitManager()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        app.Authorize(managerToken);
        var task = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Timeline API task",
            UserIds = new List<Guid> { app.EmployeeId }
        });

        app.Authorize(employeeToken);
        await app.PostJsonAsync<CommentDto>("/api/comments", new CreateCommentDto
        {
            TaskId = task.Id,
            Content = "Timeline API comment"
        });
        var timeline = await app.GetJsonAsync<TimelinePageDto>(
            $"/api/tasks/{task.Id}/timeline?size=20");

        Assert.Contains(timeline.Items, item => item.Type == TimelineEventTypes.TaskCreated);
        Assert.Contains(timeline.Items, item => item.Type == TimelineEventTypes.AssignmentAdded);
        Assert.Contains(timeline.Items, item => item.Type == TimelineEventTypes.CommentAdded);

        await app.SeedManagerInOtherUnitAsync();
        var otherManagerToken = await app.LoginAsync("manager-other-it", "Password@123");
        app.Authorize(otherManagerToken);
        var forbiddenResponse = await app.Client.GetAsync($"/api/tasks/{task.Id}/timeline");

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task TimelineEndpoint_InvalidSizeUsesValidationProblemDetails()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        app.Authorize(managerToken);
        var task = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Timeline validation task",
            UserIds = new List<Guid> { app.EmployeeId }
        });

        var response = await app.Client.GetAsync($"/api/tasks/{task.Id}/timeline?size=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
