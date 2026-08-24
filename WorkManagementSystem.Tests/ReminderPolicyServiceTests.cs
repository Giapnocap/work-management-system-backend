using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class ReminderPolicyServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026,
        8,
        20,
        10,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task Manager_CanUpsertOwnUnitPolicyAndReadEffectiveScopes()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var service = TestFactory.CreateReminderPolicyService(
            context,
            new FixedTimeProvider(Now));
        var policyId = Guid.NewGuid();

        var created = await service.UpsertAsync(policyId, new UpsertReminderPolicyDto
        {
            ScopeType = ReminderPolicyScope.Unit.ToString(),
            ScopeId = fixture.Unit.Id,
            BeforeDueHours = 12,
            OverdueEscalationHours = 6,
            NotifyAssignee = true,
            NotifyManager = true,
            IsActive = true
        }, fixture.Manager.Id);
        var visible = await service.GetAsync(fixture.Manager.Id);

        Assert.Equal(policyId, created.Id);
        Assert.Equal(fixture.Unit.Id, created.ScopeId);
        Assert.Equal(12, created.BeforeDueHours);
        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, item => item.ScopeType == ReminderPolicyScope.Global.ToString());
        Assert.Contains(visible, item => item.Id == policyId);
    }

    [Fact]
    public async Task Manager_CannotManageGlobalPolicy()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var service = TestFactory.CreateReminderPolicyService(
            context,
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpsertAsync(
            fixture.Policy.Id,
            new UpsertReminderPolicyDto
            {
                ScopeType = ReminderPolicyScope.Global.ToString(),
                BeforeDueHours = 48,
                OverdueEscalationHours = 24,
                RowVersion = fixture.Policy.RowVersion
            },
            fixture.Manager.Id));
    }

    [Fact]
    public async Task Manager_CannotCreateProjectPolicyOutsideOwnUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other policy unit" };
        var otherProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Other policy project",
            UnitId = otherUnit.Id,
            CreatedBy = fixture.Manager.Id,
            CreatedAt = Now.UtcDateTime
        };
        context.Units.Add(otherUnit);
        context.Projects.Add(otherProject);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReminderPolicyService(
            context,
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpsertAsync(
            Guid.NewGuid(),
            new UpsertReminderPolicyDto
            {
                ScopeType = ReminderPolicyScope.Project.ToString(),
                ScopeId = otherProject.Id,
                BeforeDueHours = 24,
                OverdueEscalationHours = 24
            },
            fixture.Manager.Id));
    }

    [Fact]
    public async Task Admin_CanUpdateGlobalPolicyWithConcurrencyToken()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "deadline-admin",
            FullName = "Deadline Admin",
            EmployeeCode = "ADM-DEADLINE",
            PasswordHash = "not-used-by-test",
            Role = SystemRoles.Admin,
            IsApproved = true,
            JoinedUnitAt = Now.UtcDateTime.AddDays(-30)
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReminderPolicyService(
            context,
            new FixedTimeProvider(Now));

        var updated = await service.UpsertAsync(
            fixture.Policy.Id,
            new UpsertReminderPolicyDto
            {
                ScopeType = ReminderPolicyScope.Global.ToString(),
                BeforeDueHours = 48,
                OverdueEscalationHours = 12,
                NotifyAssignee = true,
                NotifyManager = true,
                IsActive = true,
                RowVersion = fixture.Policy.RowVersion
            },
            admin.Id);

        Assert.Equal(48, updated.BeforeDueHours);
        Assert.Equal(12, updated.OverdueEscalationHours);
        Assert.Equal(Now.UtcDateTime, updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task Upsert_RejectsInvalidScopeShapeAtServiceBoundary()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var service = TestFactory.CreateReminderPolicyService(
            context,
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessException>(() => service.UpsertAsync(
            Guid.NewGuid(),
            new UpsertReminderPolicyDto
            {
                ScopeType = ReminderPolicyScope.Unit.ToString(),
                ScopeId = null,
                BeforeDueHours = 24,
                OverdueEscalationHours = 24
            },
            fixture.Manager.Id));
    }
}
