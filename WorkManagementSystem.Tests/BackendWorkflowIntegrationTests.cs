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
        var period = await app.PostJsonAsync<KpiPeriodDto>("/api/kpi-periods", new CreateKpiPeriodDto
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
        var invalidRejection = await app.Client.PostAsJsonAsync("/api/review", new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRejection.StatusCode);
        Assert.Equal("application/problem+json", invalidRejection.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "Submitted",
            (await app.GetJsonAsync<TaskDto>($"/api/tasks/{task.Id}")).Status);

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

        var history = await app.GetJsonAsync<List<TaskHistoryDto>>($"/api/tasks/{task.Id}/history");
        Assert.Contains(history, item =>
            item.FieldName == "ProgressStatus" &&
            item.RelatedEntityId == progress.Id &&
            item.NewValue == "Approved" &&
            item.Reason == "Accepted");
        var timeline = await app.GetJsonAsync<TimelinePageDto>($"/api/tasks/{task.Id}/timeline?size=50");
        Assert.Contains(timeline.Items, item =>
            item.Type == TimelineEventTypes.ProgressStatusChanged &&
            item.RelatedEntityId == progress.Id &&
            item.Metadata?.Reason == "Accepted");

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

        var forbiddenDashboard = await app.Client.GetAsync($"/api/kpi-periods/{period.Id}/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDashboard.StatusCode);

        app.Authorize(managerToken);
        var managerKpiDashboard = await app.GetJsonAsync<KpiDashboardDto>(
            $"/api/kpi-periods/{period.Id}/dashboard");
        Assert.Equal("Unit", managerKpiDashboard.Scope);
        Assert.Equal(app.UnitId, managerKpiDashboard.ScopeUnitId);
        Assert.Contains(managerKpiDashboard.Users, user => user.UserId == app.EmployeeId);
        Assert.True(managerKpiDashboard.Summary.Throughput >= 1);
        Assert.Equal("1.0", managerKpiDashboard.Formula.Version);

        app.Authorize(adminToken);
        var adminKpiDashboard = await app.GetJsonAsync<KpiDashboardDto>(
            $"/api/kpi-periods/{period.Id}/dashboard");
        Assert.Equal("Organization", adminKpiDashboard.Scope);
        Assert.Null(adminKpiDashboard.ScopeUnitId);
        Assert.False(adminKpiDashboard.Formula.UsedForAutomaticHrDecisions);
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
    public async Task Manager_CannotReadOrReviewTaskOutsideCurrentUnit()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        app.Authorize(managerToken);
        var task = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Cross-unit access boundary",
            Description = "Task owned by the integration unit",
            UserIds = new List<Guid> { app.EmployeeId },
            RequiresReview = true,
            DueDate = DateTime.UtcNow.AddDays(2)
        });

        app.Authorize(employeeToken);
        var uploadedFile = await app.UploadTextFileAsync(task.Id);
        var progress = await app.PostJsonAsync<ProgressDto>("/api/progress", new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            Description = "Ready for review",
            HoursSpent = 1,
            FileId = uploadedFile.Id
        });
        Assert.Equal("Submitted", progress.Status);

        await app.SeedManagerInOtherUnitAsync();
        var otherManagerToken = await app.LoginAsync("manager-other-it", "Password@123");
        app.Authorize(otherManagerToken);

        var readResponse = await app.Client.GetAsync($"/api/tasks/{task.Id}");
        var reviewResponse = await app.Client.PostAsJsonAsync("/api/review", new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true,
            Comment = "Must not be accepted"
        });

        Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviewResponse.StatusCode);

        app.Authorize(managerToken);
        var unchangedTask = await app.GetJsonAsync<TaskDto>($"/api/tasks/{task.Id}");
        Assert.Equal("Submitted", unchangedTask.Status);
    }

    [Fact]
    public async Task TaskDependency_BlocksProgressUntilPredecessorCompletes_ThroughHttpApi()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        var employeeToken = await app.LoginAsync("employee-it", "Password@123");

        app.Authorize(managerToken);
        var project = await app.PostJsonAsync<ProjectDto>("/api/projects", new CreateProjectDto
        {
            Name = "Dependency Integration Project",
            UnitId = app.UnitId
        });
        var predecessor = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Prepare dependency input",
            ProjectId = project.Id,
            UserIds = new List<Guid> { app.EmployeeId },
            RequiresReview = false
        });
        var dependent = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Use dependency input",
            ProjectId = project.Id,
            UserIds = new List<Guid> { app.EmployeeId },
            RequiresReview = false
        });

        var dependencyResponse = await app.Client.PostAsJsonAsync(
            $"/api/tasks/{dependent.Id}/dependencies",
            new AddTaskDependencyDto { DependsOnTaskId = predecessor.Id });
        Assert.Equal(HttpStatusCode.Created, dependencyResponse.StatusCode);
        var dependency = Assert.IsType<TaskDependencyDto>(
            await dependencyResponse.Content.ReadFromJsonAsync<TaskDependencyDto>());
        Assert.Equal(predecessor.Id, dependency.DependsOnTaskId);

        var graph = await app.GetJsonAsync<TaskDependencyGraphDto>(
            $"/api/tasks/{dependent.Id}/dependency-graph");
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);

        app.Authorize(employeeToken);
        var blockedTask = await app.GetJsonAsync<TaskDto>($"/api/tasks/{dependent.Id}");
        Assert.True(blockedTask.IsBlocked);
        Assert.Equal(predecessor.Id, Assert.Single(blockedTask.BlockingTasks).Id);

        var blockedProgressResponse = await app.Client.PostAsJsonAsync(
            "/api/progress",
            new CreateProgressDto
            {
                TaskId = dependent.Id,
                Percent = 25,
                Description = "Must wait for predecessor"
            });
        Assert.Equal(HttpStatusCode.BadRequest, blockedProgressResponse.StatusCode);

        var predecessorProgress = await app.PostJsonAsync<ProgressDto>(
            "/api/progress",
            new CreateProgressDto
            {
                TaskId = predecessor.Id,
                Percent = 100,
                Description = "Dependency input is ready"
            });
        Assert.Equal("Approved", predecessorProgress.Status);

        var unblockedTask = await app.GetJsonAsync<TaskDto>($"/api/tasks/{dependent.Id}");
        Assert.False(unblockedTask.IsBlocked);
        Assert.Empty(unblockedTask.BlockingTasks);

        var acceptedProgress = await app.PostJsonAsync<ProgressDto>(
            "/api/progress",
            new CreateProgressDto
            {
                TaskId = dependent.Id,
                Percent = 25,
                Description = "Dependency is complete"
            });
        Assert.Equal("InProgress", acceptedProgress.Status);

        var history = await app.GetJsonAsync<List<TaskHistoryDto>>(
            $"/api/tasks/{dependent.Id}/history");
        Assert.Contains(history, item => item.FieldName == "DependencyAdded");
        Assert.Contains(history, item => item.FieldName == "DependencyUnblocked");
    }

    [Fact]
    public async Task Manager_CannotManageDependencyOutsideCurrentUnit_ThroughHttpApi()
    {
        await using var app = await IntegrationTestApp.CreateAsync();

        var managerToken = await app.LoginAsync("manager-it", "Password@123");
        app.Authorize(managerToken);
        var predecessor = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Scoped predecessor",
            UserIds = new List<Guid> { app.EmployeeId }
        });
        var dependent = await app.PostJsonAsync<TaskDto>("/api/tasks", new CreateTaskDto
        {
            Title = "Scoped dependent",
            UserIds = new List<Guid> { app.EmployeeId }
        });
        await app.PostJsonAsync<TaskDependencyDto>(
            $"/api/tasks/{dependent.Id}/dependencies",
            new AddTaskDependencyDto { DependsOnTaskId = predecessor.Id });

        await app.SeedManagerInOtherUnitAsync();
        var otherManagerToken = await app.LoginAsync("manager-other-it", "Password@123");
        app.Authorize(otherManagerToken);

        var addResponse = await app.Client.PostAsJsonAsync(
            $"/api/tasks/{dependent.Id}/dependencies",
            new AddTaskDependencyDto { DependsOnTaskId = predecessor.Id });
        var removeResponse = await app.Client.DeleteAsync(
            $"/api/tasks/{dependent.Id}/dependencies/{predecessor.Id}");
        var listResponse = await app.Client.GetAsync(
            $"/api/tasks/{dependent.Id}/dependencies");
        var graphResponse = await app.Client.GetAsync(
            $"/api/tasks/{dependent.Id}/dependency-graph");

        Assert.Equal(HttpStatusCode.Forbidden, addResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, graphResponse.StatusCode);
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
