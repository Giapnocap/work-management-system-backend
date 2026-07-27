using System.Net;
using System.Net.Http.Json;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class BackendWorkflowIntegrationTests
{
    [Fact]
    public async Task Manager_User_TaskCompletionFlow_WorksThroughHttpApi()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var adminToken = await app.LoginAsync("admin-it", "Password@123");
        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        var now = DateTime.UtcNow;
        app.Authorize(adminToken);
        await app.PostJsonAsync<KpiPeriodDto>("/api/kpi-periods", new CreateKpiPeriodDto
        {
            Name = $"KPI {now:MM/yyyy}",
            Type = "Monthly",
            StartDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1)
                .AddDays(-1)
        });

        app.Authorize(managerToken);
        var project = await app.PostJsonAsync<ProjectDto>("/api/projects", new CreateProjectDto
        {
            Name = "Integration Project",
            Description = "Project created by integration test",
            UnitId = app.UnitId
        });

        Assert.Equal(app.UnitId, project.UnitId);
        Assert.Equal(4, project.StatusCounts.Count);
        Assert.All(project.StatusCounts, status => Assert.Equal(0, status.Count));

        var task = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Finish backend workflow",
            Description = "Create, report and approve through HTTP",
            ProjectId = project.Id,
            UserIds = new List<Guid> { app.EmployeeId },
            Priority = "High",
            RequiresReview = true,
            DueDate = DateTime.UtcNow.AddDays(2)
        });

        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal("NotStarted", task.Status);
        Assert.Contains(task.Assignees, assignee => assignee.Id == app.EmployeeId);

        var projectsAfterTask = await app.GetJsonAsync<List<ProjectDto>>("/api/projects");
        var projectAfterTask = Assert.Single(projectsAfterTask, item => item.Id == project.Id);
        Assert.Equal(1, projectAfterTask.StatusCounts.Single(item => item.Status == "NotStarted").Count);

        app.Authorize(employeeToken);
        var uploadedFile = await app.UploadTextFileAsync(task.Id);
        Assert.Equal(task.Id, uploadedFile.TaskId);
        Assert.Equal(app.EmployeeId, uploadedFile.UploadedBy);

        var progress = await app.PostJsonAsync<ProgressDto>("/api/progress", new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            Description = "Completed and ready for review",
            HoursSpent = 2,
            FileId = uploadedFile.Id
        });

        Assert.Equal(task.Id, progress.TaskId);
        Assert.Equal(100, progress.Percent);
        Assert.Equal("Submitted", progress.Status);

        var submittedTask = await app.GetJsonAsync<TaskDto>($"/api/tasks/{task.Id}");
        Assert.Equal("Submitted", submittedTask.Status);

        app.Authorize(managerToken);
        var review = await app.PostJsonAsync<ReviewDto>("/api/review", new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true,
            Comment = "Accepted"
        });

        Assert.True(review.Approve);

        var approvedTask = await app.GetJsonAsync<TaskDto>($"/api/tasks/{task.Id}");
        Assert.Equal("Approved", approvedTask.Status);
        Assert.Equal(2, approvedTask.ActualHours);
        Assert.Equal(app.EmployeeId, approvedTask.CompletedBy);

        var projectsAfterReview = await app.GetJsonAsync<List<ProjectDto>>("/api/projects");
        var projectAfterReview = Assert.Single(projectsAfterReview, item => item.Id == project.Id);
        Assert.Equal(1, projectAfterReview.StatusCounts.Single(item => item.Status == "Approved").Count);

        app.Authorize(employeeToken);
        var notifications = await app.GetJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Contains(
            notifications,
            notification => notification.Message.Contains("phe duyet", StringComparison.OrdinalIgnoreCase));

        var performance = await app.GetJsonAsync<PerformanceDto>($"/api/users/performance/{app.EmployeeId}");
        Assert.Equal(app.EmployeeId, performance.UserId);
        Assert.True(performance.TotalTasks >= 1);
        Assert.True(performance.CompletedOnTime >= 1);
        Assert.True(performance.Score >= 0);
    }

    [Fact]
    public async Task Employee_CannotCreateProjectOrTask()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var employeeToken = await app.LoginAsync("employee-it", "Password@123");
        app.Authorize(employeeToken);

        var projectResponse = await app.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "Forbidden Project",
            UnitId = app.UnitId
        });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, projectResponse.StatusCode);

        var taskResponse = await app.Client.PostAsJsonAsync("/api/tasks", new CreateTaskDto
        {
            Title = "Forbidden Task",
            UserIds = new List<Guid> { app.EmployeeId }
        });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, taskResponse.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotChangeDepartmentMembership()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        app.Authorize(managerToken);

        var addResponse = await app.Client.PostAsJsonAsync(
            $"/api/units/{app.UnitId}/members",
            new MemberDto { UserId = app.EmployeeId });
        var removeResponse = await app.Client.DeleteAsync(
            $"/api/units/{app.UnitId}/members/{app.EmployeeId}");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, addResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, removeResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEmployee_RevokesSessionAndKeepsLockedKpiVisibleToHistoricalManager()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var adminToken = await app.LoginAsync("admin-it", "Password@123");
        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");
        var now = DateTime.UtcNow;

        app.Authorize(adminToken);
        var period = await app.PostJsonAsync<KpiPeriodDto>("/api/kpi-periods", new CreateKpiPeriodDto
        {
            Name = $"KPI {now:MM/yyyy}",
            Type = "Monthly",
            StartDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1)
                .AddTicks(-1)
        });

        var deleteResponse = await app.Client.DeleteAsync($"/api/users/{app.EmployeeId}");
        await deleteResponse.AssertSuccessAsync();

        app.Authorize(employeeToken);
        var revokedSessionResponse = await app.Client.GetAsync("/api/kpi-periods");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSessionResponse.StatusCode);

        app.Authorize(adminToken);
        var lockedResults = await app.PostJsonAsync<List<PerformanceDto>>(
            $"/api/kpi-periods/{period.Id}/lock",
            new { });
        Assert.Contains(lockedResults, result => result.UserId == app.EmployeeId);

        app.Authorize(managerToken);
        var historicalPerformance = await app.GetJsonAsync<PerformanceDto>(
            $"/api/users/performance/{app.EmployeeId}?periodId={period.Id}");

        Assert.Equal(app.EmployeeId, historicalPerformance.UserId);
        Assert.Equal("Integration Employee", historicalPerformance.FullName);
        Assert.Equal("EMP9999", historicalPerformance.EmployeeCode);
        Assert.Equal("Integration Unit", historicalPerformance.UnitName);
        Assert.True(historicalPerformance.IsLocked);
    }
}
