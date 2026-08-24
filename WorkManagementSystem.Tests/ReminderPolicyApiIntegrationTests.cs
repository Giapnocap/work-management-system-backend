using System.Net;
using System.Net.Http.Json;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class ReminderPolicyApiIntegrationTests
{
    [Fact]
    public async Task Manager_CanUpsertOwnUnitPolicy_EmployeeIsForbidden()
    {
        await using var app = await IntegrationTestApp.CreateAsync();
        var policyId = Guid.NewGuid();
        var request = new UpsertReminderPolicyDto
        {
            ScopeType = ReminderPolicyScope.Unit.ToString(),
            ScopeId = app.UnitId,
            BeforeDueHours = 24,
            OverdueEscalationHours = 12,
            NotifyAssignee = true,
            NotifyManager = true,
            IsActive = true
        };
        app.Authorize(await app.LoginAsync("manager-it", "Password@123"));

        var response = await app.Client.PutAsJsonAsync(
            $"/api/management/reminder-policies/{policyId}",
            request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policies = await app.GetJsonAsync<List<ReminderPolicyDto>>(
            "/api/management/reminder-policies");
        Assert.Contains(policies, policy => policy.Id == policyId && policy.ScopeId == app.UnitId);

        app.Authorize(await app.LoginAsync("employee-it", "Password@123"));
        var forbidden = await app.Client.PutAsJsonAsync(
            $"/api/management/reminder-policies/{Guid.NewGuid()}",
            request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
