using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class RecurringTaskServiceTests
{
    private static readonly FixedTimeProvider Clock = new(
        new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Create_NormalizesUtcAndStoresExplicitAssignee()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var service = TestFactory.CreateRecurringTaskService(context, Clock);

        var result = await service.CreateAsync(new CreateRecurringTaskDto
        {
            Title = "  Báo cáo đầu tuần  ",
            Description = "  Tổng hợp công việc  ",
            Priority = "High",
            PlannedEffortHours = 3.5m,
            RecurrenceType = "Weekly",
            Interval = 1,
            DayOfWeek = 1,
            NextRunAtUtc = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(7)),
            ProjectId = fixture.Project.Id,
            UserIds = new List<Guid> { fixture.Employee.Id, fixture.Employee.Id }
        }, fixture.Manager.Id);

        Assert.Equal("Báo cáo đầu tuần", result.Title);
        Assert.Equal(new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc), result.NextRunAtUtc);
        Assert.Equal("Weekly", result.RecurrenceType);
        Assert.Equal(fixture.Employee.Id, Assert.Single(result.Assignees).Id);
        Assert.True(result.IsActive);
        Assert.NotEmpty(result.RowVersion);
        Assert.Single(await context.AuditLogs.Where(log => log.EntityId == result.Id).ToListAsync());
    }

    [Fact]
    public async Task Create_WithoutExplicitAssigneeKeepsDynamicUnitAssignment()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var service = TestFactory.CreateRecurringTaskService(context, Clock);

        var result = await service.CreateAsync(CreateDailyRequest(), fixture.Manager.Id);

        Assert.Empty(result.Assignees);
        Assert.Empty(await context.RecurringTaskAssignees.ToListAsync());
    }

    [Fact]
    public async Task Update_WithSameAssigneeDoesNotCreateDuplicateRows()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var service = TestFactory.CreateRecurringTaskService(context, Clock);
        var request = CreateDailyRequest();
        request.UserIds.Add(fixture.Employee.Id);
        var created = await service.CreateAsync(request, fixture.Manager.Id);

        var updated = await service.UpdateAsync(created.Id, new UpdateRecurringTaskDto
        {
            RowVersion = created.RowVersion,
            Title = "Updated recurring work",
            Description = created.Description,
            Priority = created.Priority,
            RequiresReview = created.RequiresReview,
            PlannedEffortHours = created.PlannedEffortHours,
            RecurrenceType = created.RecurrenceType,
            Interval = created.Interval,
            NextRunAtUtc = new DateTimeOffset(created.NextRunAtUtc.AddDays(1)),
            ProjectId = created.ProjectId,
            UserIds = new List<Guid> { fixture.Employee.Id }
        }, fixture.Manager.Id);

        Assert.Equal("Updated recurring work", updated.Title);
        Assert.Single(await context.RecurringTaskAssignees
            .Where(assignee => assignee.TemplateId == created.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task PauseAndResume_AreIdempotentAndPreserveSchedule()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var service = TestFactory.CreateRecurringTaskService(context, Clock);
        var created = await service.CreateAsync(CreateDailyRequest(), fixture.Manager.Id);

        var paused = await service.PauseAsync(created.Id, fixture.Manager.Id);
        var pausedAgain = await service.PauseAsync(created.Id, fixture.Manager.Id);
        var resumed = await service.ResumeAsync(created.Id, fixture.Manager.Id);

        Assert.False(paused.IsActive);
        Assert.False(pausedAgain.IsActive);
        Assert.True(resumed.IsActive);
        Assert.Equal(created.NextRunAtUtc, resumed.NextRunAtUtc);
    }

    [Fact]
    public async Task ExplicitDefaultAssignee_CannotBeTransferredSilently()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Clock.GetUtcNow().UtcDateTime.AddDays(1));
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other recurring unit" };
        context.Units.Add(otherUnit);
        await context.SaveChangesAsync();
        var assignmentService = TestFactory.CreateUserTaskAssignmentService(context);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            assignmentService.EnsureCanChangeAssignmentAsync(
                fixture.Employee,
                SystemRoles.User,
                otherUnit.Id));

        Assert.Contains("lịch công việc định kỳ", exception.Message);
    }

    [Fact]
    public async Task ProjectWithRecurringTemplate_CannotBeArchived()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Clock.GetUtcNow().UtcDateTime.AddDays(1));
        var projectService = TestFactory.CreateProjectService(context);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            projectService.ArchiveProject(fixture.Project.Id, fixture.Manager.Id));

        Assert.Contains("lịch công việc định kỳ", exception.Message);
        Assert.False(fixture.Project.IsArchived);
    }

    [Fact]
    public async Task UnitWithRecurringTemplate_CannotBeDeleted()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Clock.GetUtcNow().UtcDateTime.AddDays(1));
        fixture.Manager.UnitId = null;
        fixture.Employee.UnitId = null;
        context.UserUnits.RemoveRange(context.UserUnits);
        await context.SaveChangesAsync();
        var unitService = TestFactory.CreateUnitService(context);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            unitService.Delete(fixture.Unit.Id));

        Assert.Contains("lịch công việc định kỳ", exception.Message);
        Assert.False(fixture.Unit.IsDeleted);
    }

    private static CreateRecurringTaskDto CreateDailyRequest()
    {
        return new CreateRecurringTaskDto
        {
            Title = "Daily recurring work",
            Description = "Recurring task",
            Priority = "Medium",
            RequiresReview = true,
            PlannedEffortHours = 2m,
            RecurrenceType = "Daily",
            Interval = 1,
            NextRunAtUtc = Clock.GetUtcNow().AddDays(1)
        };
    }
}
