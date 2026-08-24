using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class WorkloadApiIntegrationTests
{
    private static readonly DateTime WeekStart = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekEnd = WeekStart.AddDays(6);

    [Fact]
    public async Task Swagger_ExposesWorkloadCapacityAndPreviewContracts()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var swagger = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Contains("/api/management/workload", swagger.Paths.Keys);
        Assert.Contains("/api/management/workload/{userId}", swagger.Paths.Keys);
        Assert.Contains("/api/users/{userId}/capacity", swagger.Paths.Keys);
        Assert.Contains("/api/tasks/assignments/preview", swagger.Paths.Keys);
        Assert.Contains(
            OperationType.Put,
            swagger.Paths["/api/users/{userId}/capacity"].Operations.Keys);
        Assert.Contains(
            OperationType.Post,
            swagger.Paths["/api/tasks/assignments/preview"].Operations.Keys);
    }

    [Fact]
    public async Task Manager_CanConfigureCapacityViewWorkloadAndReceiveNonBlockingWarning()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));

        var capacityResponse = await app.Client.PutAsJsonAsync(
            $"/api/users/{app.EmployeeId}/capacity",
            new UpdateUserCapacityDto
            {
                WeeklyCapacityHours = 40m,
                EffectiveFrom = WeekStart
            });
        Assert.Equal(HttpStatusCode.OK, capacityResponse.StatusCode);

        var previewResponse = await app.Client.PostAsJsonAsync(
            "/api/tasks/assignments/preview",
            new AssignmentPreviewRequestDto
            {
                PlannedEffortHours = 36m,
                StartDate = WeekStart,
                DueDate = WeekEnd,
                UserIds = new List<Guid> { app.EmployeeId }
            });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<AssignmentPreviewDto>();
        Assert.NotNull(preview);
        Assert.True(preview.HasWarning);
        Assert.Equal("Overloaded", Assert.Single(preview.Assignees).ProjectedLevel);

        var taskResponse = await app.Client.PostAsJsonAsync("/api/tasks", new CreateTaskDto
        {
            Title = "Workload API task",
            PlannedEffortHours = 36m,
            StartDate = WeekStart,
            DueDate = WeekEnd,
            UserIds = new List<Guid> { app.EmployeeId }
        });
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var task = await taskResponse.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(task);
        Assert.Equal(36m, task.PlannedEffortHours);
        Assert.Single(task.WorkloadWarnings);

        var from = Uri.EscapeDataString(WeekStart.ToString("O"));
        var to = Uri.EscapeDataString(WeekEnd.ToString("O"));
        var workload = await app.GetJsonAsync<WorkloadSummaryDto>(
            $"/api/management/workload?from={from}&to={to}");
        var employee = Assert.Single(workload.Users);
        Assert.Equal(app.EmployeeId, employee.UserId);
        Assert.Equal(90m, employee.WorkloadPercent);
        Assert.Equal("Overloaded", employee.Level);
    }

    [Fact]
    public async Task WorkloadEndpoints_EnforceRoleAndUnitScope()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var other = await app.SeedEmployeeInOtherUnitAsync();

        app.Authorize(await app.LoginAsync("employee-it", "Password@123"));
        var employeeResponse = await app.Client.GetAsync("/api/management/workload");
        Assert.Equal(HttpStatusCode.Forbidden, employeeResponse.StatusCode);

        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));
        var crossUnitRead = await app.Client.GetAsync(
            $"/api/management/workload/{other.EmployeeId}");
        Assert.Equal(HttpStatusCode.Forbidden, crossUnitRead.StatusCode);

        var crossUnitUpdate = await app.Client.PutAsJsonAsync(
            $"/api/users/{other.EmployeeId}/capacity",
            new UpdateUserCapacityDto { WeeklyCapacityHours = 40m });
        Assert.Equal(HttpStatusCode.Forbidden, crossUnitUpdate.StatusCode);

        app.Authorize(await app.LoginAsync("admin-it", "Password@123"));
        var adminUpdate = await app.Client.PutAsJsonAsync(
            $"/api/users/{other.EmployeeId}/capacity",
            new UpdateUserCapacityDto { WeeklyCapacityHours = 35m });
        Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);
    }
}
