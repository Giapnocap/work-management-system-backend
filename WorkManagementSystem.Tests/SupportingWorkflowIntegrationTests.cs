using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class SupportingWorkflowIntegrationTests
{
    [Fact]
    public async Task AuditLogs_AreRecordedForProjectCreationAndRestrictedToAdmin()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");
        var adminToken = await app.LoginAsync("admin-it", "Password@123");

        app.Authorize(managerToken);
        var project = await app.PostJsonAsync<ProjectDto>("/api/projects", new CreateProjectDto
        {
            Name = "Audited project",
            Description = "Audit integration coverage",
            UnitId = app.UnitId
        });

        app.Authorize(employeeToken);
        var forbiddenResponse = await app.Client.GetAsync(
            $"/api/audit-logs?entityType=Project&entityId={project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        app.Authorize(adminToken);
        var page = await app.GetJsonAsync<AuditLogPageDto>(
            $"/api/audit-logs?entityType=Project&entityId={project.Id}");

        var audit = Assert.Single(page.Data);
        Assert.Equal("Created", audit.Action);
        Assert.Equal(app.ManagerId, audit.ActorUserId);
    }

    [Fact]
    public async Task InvalidLogin_ReturnsStableNonDisclosureErrorContract()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = "employee-it",
            Password = "WrongPassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = payload.RootElement;
        Assert.Equal((int)HttpStatusCode.Unauthorized, root.GetProperty("status").GetInt32());
        Assert.Equal("invalid_credentials", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Equal(string.Empty, root.GetProperty("detail").GetString());
        Assert.DoesNotContain("employee-it", root.GetProperty("message").GetString() ?? string.Empty);
    }

    [Fact]
    public async Task ProfileAndPassword_WorkflowPersistsChangesAndRotatesCredentials()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var employeeToken = await app.LoginAsync("employee-it", "Password@123");
        app.Authorize(employeeToken);

        var initialProfile = await app.GetJsonAsync<ProfileDto>("/api/profile");
        Assert.Equal("Integration Employee", initialProfile.FullName);

        var updateResponse = await app.Client.PutAsJsonAsync("/api/profile", new ProfileDto
        {
            FullName = "  Updated Employee  ",
            Email = "  employee.updated@example.test  ",
            PhoneNumber = "  0900000000  "
        });
        await updateResponse.AssertSuccessAsync();
        var updatePayload = await updateResponse.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(updatePayload);
        Assert.Equal("Updated Employee", updatePayload.FullName);
        Assert.Equal("employee.updated@example.test", updatePayload.Email);

        var updatedProfile = await app.GetJsonAsync<ProfileDto>("/api/profile");
        Assert.Equal("Updated Employee", updatedProfile.FullName);
        Assert.Equal("employee.updated@example.test", updatedProfile.Email);
        Assert.Equal("0900000000", updatedProfile.PhoneNumber);

        var wrongPasswordResponse = await app.Client.PostAsJsonAsync("/api/change-password", new ChangePasswordDto
        {
            OldPassword = "WrongPassword",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPasswordResponse.StatusCode);

        var changeResponse = await app.Client.PostAsJsonAsync("/api/change-password", new ChangePasswordDto
        {
            OldPassword = "Password@123",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });
        await changeResponse.AssertSuccessAsync();

        var staleTokenResponse = await app.Client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, staleTokenResponse.StatusCode);

        var oldLoginResponse = await app.Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Username = "employee-it",
            Password = "Password@123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newToken = await app.LoginAsync("employee-it", "NewPassword@123");
        app.Authorize(newToken);
        var currentProfile = await app.GetJsonAsync<ProfileDto>("/api/profile");
        Assert.False(string.IsNullOrWhiteSpace(newToken));
        Assert.Equal("Updated Employee", currentProfile.FullName);
    }

    [Fact]
    public async Task TaskCollaboration_EnforcesSubTaskOwnershipAndCommentLifecycle()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        app.Authorize(managerToken);
        var task = await CreateAssignedTask(app, "Collaboration task");

        var subTask = await app.PostJsonAsync<SubTaskDto>("/api/subtasks", new CreateSubTaskDto
        {
            TaskId = task.Id,
            Title = "  Prepare report  "
        });
        Assert.Equal("Prepare report", subTask.Title);

        var duplicateResponse = await app.Client.PostAsJsonAsync("/api/subtasks", new CreateSubTaskDto
        {
            TaskId = task.Id,
            Title = "Prepare report"
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        app.Authorize(employeeToken);
        var forbiddenAddResponse = await app.Client.PostAsJsonAsync("/api/subtasks", new CreateSubTaskDto
        {
            TaskId = task.Id,
            Title = "Employee cannot add"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAddResponse.StatusCode);

        var toggleResponse = await app.Client.PatchAsync($"/api/subtasks/{subTask.Id}/toggle", null);
        await toggleResponse.AssertSuccessAsync();

        var subTasks = await app.GetJsonAsync<List<SubTaskDto>>($"/api/subtasks/task/{task.Id}");
        Assert.True(Assert.Single(subTasks).IsCompleted);

        var comment = await app.PostJsonAsync<CommentDto>("/api/comments", new CreateCommentDto
        {
            TaskId = task.Id,
            Content = "  Ready for review  "
        });
        Assert.Equal("Ready for review", comment.Content);

        app.Authorize(managerToken);
        var reactionResponse = await app.Client.PostAsJsonAsync($"/api/comments/{comment.Id}/react", "like");
        await reactionResponse.AssertSuccessAsync();

        var invalidReactionResponse = await app.Client.PostAsJsonAsync($"/api/comments/{comment.Id}/react", "   ");
        Assert.Equal(HttpStatusCode.BadRequest, invalidReactionResponse.StatusCode);
        using (var payload = await JsonDocument.ParseAsync(await invalidReactionResponse.Content.ReadAsStreamAsync()))
        {
            Assert.Equal("business_error", payload.RootElement.GetProperty("code").GetString());
        }

        var seenResponse = await app.Client.PostAsync($"/api/comments/task/{task.Id}/seen", null);
        await seenResponse.AssertSuccessAsync();

        var comments = await app.GetJsonAsync<List<CommentDto>>($"/api/comments/{task.Id}");
        var savedComment = Assert.Single(comments);
        Assert.Equal("like", savedComment.MyReaction);
        Assert.Equal(1, Assert.Single(savedComment.Reactions).Count);
        Assert.Contains("Integration Manager", savedComment.SeenByUserFullNames);

        var deleteResponse = await app.Client.DeleteAsync($"/api/comments/{comment.Id}");
        await deleteResponse.AssertSuccessAsync();
        Assert.Empty(await app.GetJsonAsync<List<CommentDto>>($"/api/comments/{task.Id}"));
    }

    [Fact]
    public async Task DashboardAndExport_ReturnRoleScopedOperationalData()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        app.Authorize(managerToken);
        await CreateAssignedTask(app, "Dashboard task");

        var dashboard = await app.GetJsonAsync<ManagerDashboardDto>("/api/dashboard/manager");
        Assert.Equal("Integration Unit", dashboard.UnitName);
        Assert.Equal(1, dashboard.TotalMembers);
        Assert.Equal(1, dashboard.TotalTasks);
        Assert.Equal(1, dashboard.TaskPending);

        var taskExport = await app.Client.GetAsync("/api/export/tasks");
        await taskExport.AssertSuccessAsync();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            taskExport.Content.Headers.ContentType?.MediaType);
        Assert.True((await taskExport.Content.ReadAsByteArrayAsync()).Length > 0);

        var progressExport = await app.Client.GetAsync("/api/export/progress");
        await progressExport.AssertSuccessAsync();
        Assert.True((await progressExport.Content.ReadAsByteArrayAsync()).Length > 0);

        app.Authorize(employeeToken);
        var forbiddenExport = await app.Client.GetAsync("/api/export/tasks");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenExport.StatusCode);
    }

    private static async Task<TaskDto> CreateAssignedTask(IntegrationTestApp app, string title)
    {
        var project = await app.PostJsonAsync<ProjectDto>("/api/projects", new CreateProjectDto
        {
            Name = $"{title} project",
            UnitId = app.UnitId
        });

        return await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = title,
            ProjectId = project.Id,
            UserIds = new List<Guid> { app.EmployeeId },
            RequiresReview = true
        });
    }
}
