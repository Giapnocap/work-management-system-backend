using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public sealed class RecurringTaskSchedulerTests
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
    public async Task ProcessDue_CreatesCompleteTaskOccurrenceAndAdvancesSchedule()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var template = await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Now.UtcDateTime);
        var notifications = new TestNotificationService();
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var result = await scheduler.ProcessDueAsync();

        Assert.Equal(1, result.TemplatesProcessed);
        Assert.Equal(1, result.OccurrencesGenerated);
        Assert.Equal(0, result.Failures);
        var occurrence = await context.GeneratedTaskOccurrences.SingleAsync();
        var task = await context.Tasks.SingleAsync(candidate => candidate.Id == occurrence.TaskId);
        Assert.Equal(template.Id, occurrence.TemplateId);
        Assert.Equal(Now.UtcDateTime, occurrence.ScheduledForUtc);
        Assert.Equal(Now.UtcDateTime, task.StartDate);
        Assert.Equal(fixture.Project.Id, task.ProjectId);
        Assert.Equal(4m, task.PlannedEffortHours);
        Assert.Equal(fixture.Employee.Id, (await context.TaskAssignees.SingleAsync()).UserId);
        Assert.Equal("RecurringTaskGenerated", (await context.TaskHistories.SingleAsync()).FieldName);
        Assert.Single(notifications.Sent);

        var reloadedTemplate = await context.RecurringTaskTemplates.SingleAsync();
        Assert.Equal(Now.UtcDateTime, reloadedTemplate.LastGeneratedAtUtc);
        Assert.Equal(Now.UtcDateTime.AddDays(1), reloadedTemplate.NextRunAtUtc);
    }

    [Fact]
    public async Task ProcessDue_WhenRerun_DoesNotGenerateDuplicateOccurrence()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(context, fixture, Now.UtcDateTime);
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now));

        var first = await scheduler.ProcessDueAsync();
        var second = await scheduler.ProcessDueAsync();

        Assert.Equal(1, first.OccurrencesGenerated);
        Assert.Equal(0, second.OccurrencesGenerated);
        Assert.Single(await context.GeneratedTaskOccurrences.ToListAsync());
        Assert.Single(await context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task ProcessDue_WhenLate_UsesBoundedCatchUpWithoutSkippingSchedule()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Now.UtcDateTime.AddDays(-3));
        var options = new RecurringTaskOptions
        {
            MaxCatchUpOccurrencesPerTemplate = 2
        };
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now),
            options: options);

        var first = await scheduler.ProcessDueAsync();
        var afterFirst = await context.RecurringTaskTemplates.AsNoTracking().SingleAsync();
        var second = await scheduler.ProcessDueAsync();
        var afterSecond = await context.RecurringTaskTemplates.AsNoTracking().SingleAsync();

        Assert.Equal(2, first.OccurrencesGenerated);
        Assert.Equal(Now.UtcDateTime.AddDays(-1), afterFirst.NextRunAtUtc);
        Assert.Equal(2, second.OccurrencesGenerated);
        Assert.Equal(Now.UtcDateTime.AddDays(1), afterSecond.NextRunAtUtc);
        Assert.Equal(4, await context.GeneratedTaskOccurrences.CountAsync());
    }

    [Fact]
    public async Task ProcessDue_WhenPaused_DoesNotGenerate()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Now.UtcDateTime.AddDays(-1),
            isActive: false);
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now));

        var result = await scheduler.ProcessDueAsync();

        Assert.Equal(0, result.TemplatesProcessed);
        Assert.Empty(await context.GeneratedTaskOccurrences.ToListAsync());
        Assert.Empty(await context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task ProcessDue_WithoutExplicitDefaults_SnapshotsCurrentDepartmentUsers()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Now.UtcDateTime,
            useExplicitAssignee: false);
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now));

        var result = await scheduler.ProcessDueAsync();

        Assert.Equal(1, result.OccurrencesGenerated);
        var assignee = await context.TaskAssignees.SingleAsync();
        Assert.Equal(fixture.Employee.Id, assignee.UserId);
    }

    [Fact]
    public async Task ProcessDue_WhenGenerationFails_RollsBackEveryPartAndKeepsSchedule()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await RecurringTaskTestData.SeedAsync(context);
        var template = await RecurringTaskTestData.AddTemplateAsync(
            context,
            fixture,
            Now.UtcDateTime);
        var logger = new CollectingLogger<RecurringTaskSchedulerService>();
        var scheduler = TestFactory.CreateRecurringTaskScheduler(
            context,
            new FixedTimeProvider(Now),
            new FailingNotificationService(),
            logger: logger);

        var result = await scheduler.ProcessDueAsync();

        Assert.Equal(1, result.Failures);
        Assert.Empty(await context.Tasks.AsNoTracking().ToListAsync());
        Assert.Empty(await context.GeneratedTaskOccurrences.AsNoTracking().ToListAsync());
        Assert.Empty(await context.TaskHistories.AsNoTracking().ToListAsync());
        var reloadedTemplate = await context.RecurringTaskTemplates.AsNoTracking().SingleAsync();
        Assert.Equal(Now.UtcDateTime, reloadedTemplate.NextRunAtUtc);
        Assert.Null(reloadedTemplate.LastGeneratedAtUtc);
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(template.Id, error.Properties["TemplateId"]);
    }

    [Fact]
    public async Task ProcessDue_AfterContextRestart_UsesPersistedScheduleAndRemainsIdempotent()
    {
        var databaseName = $"RecurringRestart-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid templateId;

        await using (var setupContext = TestFactory.CreateDbContext(databaseName, databaseRoot))
        {
            var fixture = await RecurringTaskTestData.SeedAsync(setupContext);
            var template = await RecurringTaskTestData.AddTemplateAsync(
                setupContext,
                fixture,
                Now.UtcDateTime);
            templateId = template.Id;
        }

        await using (var firstWorkerContext = TestFactory.CreateDbContext(databaseName, databaseRoot))
        {
            var scheduler = TestFactory.CreateRecurringTaskScheduler(
                firstWorkerContext,
                new FixedTimeProvider(Now));
            var result = await scheduler.ProcessDueAsync();
            Assert.Equal(1, result.OccurrencesGenerated);
        }

        await using (var restartedWorkerContext = TestFactory.CreateDbContext(databaseName, databaseRoot))
        {
            var scheduler = TestFactory.CreateRecurringTaskScheduler(
                restartedWorkerContext,
                new FixedTimeProvider(Now));
            var result = await scheduler.ProcessDueAsync();

            Assert.Equal(0, result.OccurrencesGenerated);
            Assert.Single(await restartedWorkerContext.GeneratedTaskOccurrences
                .Where(occurrence => occurrence.TemplateId == templateId)
                .ToListAsync());
            Assert.Single(await restartedWorkerContext.Tasks.ToListAsync());
        }
    }

    private sealed class FailingNotificationService : INotificationService
    {
        public Task AddNotification(
            Guid userId,
            string message,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated notification failure.");

        public Task<List<NotificationDto>> GetMyNotifications(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<NotificationDto>());

        public Task MarkAsRead(
            Guid notificationId,
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetUnreadCount(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
