using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public sealed class DeadlineReminderServiceTests
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
    public async Task DueInTwentyFourHours_SendsAssigneeOnce()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var first = await service.ProcessDueAsync();
        var second = await service.ProcessDueAsync();

        Assert.Equal(1, first.EventsSent);
        Assert.Equal(0, second.EventsSent);
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal(fixture.Employee.Id, sent.UserId);
        var scheduled = Assert.Single(await DeadlineReminderTestData.GetEventsAsync(context, fixture.Task.Id));
        Assert.Equal(ScheduledNotificationType.DueSoon, scheduled.Type);
        Assert.Equal(ScheduledNotificationStatus.Sent, scheduled.Status);
    }

    [Fact]
    public async Task Overdue_SendsEmployeeReminder()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(-1));
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var result = await service.ProcessDueAsync();

        Assert.Equal(1, result.EventsSent);
        Assert.Contains(notifications.Sent, item => item.UserId == fixture.Employee.Id);
        var events = await DeadlineReminderTestData.GetEventsAsync(context, fixture.Task.Id);
        Assert.Contains(events, item =>
            item.Type == ScheduledNotificationType.OverdueAssignee &&
            item.Status == ScheduledNotificationStatus.Sent);
        Assert.Contains(events, item =>
            item.Type == ScheduledNotificationType.DueSoon &&
            item.Status == ScheduledNotificationStatus.Suppressed);
    }

    [Fact]
    public async Task OverdueThreshold_EscalatesOnlyToManagersInTaskUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(-24));
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other escalation unit" };
        context.Units.Add(otherUnit);
        await context.SaveChangesAsync();
        var otherManager = await DeadlineReminderTestData.AddManagerAsync(
            context,
            otherUnit.Id,
            "Other Manager");
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var result = await service.ProcessDueAsync();

        Assert.Equal(2, result.EventsSent);
        Assert.Contains(notifications.Sent, item => item.UserId == fixture.Employee.Id);
        Assert.Contains(notifications.Sent, item => item.UserId == fixture.Manager.Id);
        Assert.DoesNotContain(notifications.Sent, item => item.UserId == otherManager.Id);
    }

    [Fact]
    public async Task Restart_UsesPersistedEventAndDoesNotDuplicateInboxNotification()
    {
        var databaseName = $"DeadlineRestart-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid taskId;
        Guid employeeId;

        await using (var firstContext = TestFactory.CreateDbContext(databaseName, databaseRoot))
        {
            var fixture = await DeadlineReminderTestData.SeedAsync(
                firstContext,
                Now.UtcDateTime.AddHours(24));
            taskId = fixture.Task.Id;
            employeeId = fixture.Employee.Id;
            var firstWorker = TestFactory.CreateDeadlineReminderService(
                firstContext,
                new FixedTimeProvider(Now));
            Assert.Equal(1, (await firstWorker.ProcessDueAsync()).EventsSent);
        }

        await using (var restartedContext = TestFactory.CreateDbContext(databaseName, databaseRoot))
        {
            var restartedWorker = TestFactory.CreateDeadlineReminderService(
                restartedContext,
                new FixedTimeProvider(Now));
            Assert.Equal(0, (await restartedWorker.ProcessDueAsync()).EventsSent);

            Assert.Single(await restartedContext.ScheduledNotifications
                .Where(notification => notification.TaskId == taskId)
                .ToListAsync());
            Assert.Single(await restartedContext.Notifications
                .Where(notification => notification.UserId == employeeId)
                .ToListAsync());
        }
    }

    [Fact]
    public async Task CompletedImmediatelyBeforeDelivery_SuppressesPendingEvent()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        context.ScheduledNotifications.Add(new ScheduledNotification
        {
            Id = Guid.NewGuid(),
            TaskId = fixture.Task.Id,
            Type = ScheduledNotificationType.DueSoon,
            ScheduledForUtc = Now.UtcDateTime,
            CreatedAtUtc = Now.UtcDateTime,
            Status = ScheduledNotificationStatus.Pending,
            EventKey = $"deadline:{fixture.Task.Id:N}:DueSoon"
        });
        fixture.Task.Status = TaskStatusEnum.Approved;
        await context.SaveChangesAsync();
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var result = await service.ProcessDueAsync();

        Assert.Equal(1, result.EventsSuppressed);
        Assert.Empty(notifications.Sent);
        Assert.Equal(
            ScheduledNotificationStatus.Suppressed,
            (await context.ScheduledNotifications.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task PolicyChange_AppliesBeforeFutureMilestoneIsCreated()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24),
            beforeDueHours: 12);
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        Assert.Equal(0, (await service.ProcessDueAsync()).EventsCreated);
        fixture.Policy.BeforeDueHours = 24;
        fixture.Policy.UpdatedAtUtc = Now.UtcDateTime;
        await context.SaveChangesAsync();

        Assert.Equal(1, (await service.ProcessDueAsync()).EventsSent);
        Assert.Single(notifications.Sent);
    }

    [Fact]
    public async Task TransientDeliveryFailure_IsPersistedAndRetriedWithinLimit()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var logger = new CollectingLogger<DeadlineReminderService>();
        var failingService = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            new FailingNotificationService(),
            logger: logger);

        var failed = await failingService.ProcessDueAsync();
        var failedEvent = await context.ScheduledNotifications.AsNoTracking().SingleAsync();

        Assert.Equal(1, failed.Failures);
        Assert.Equal(ScheduledNotificationStatus.Failed, failedEvent.Status);
        Assert.Equal(1, failedEvent.RetryCount);
        Assert.NotNull(failedEvent.LastError);
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(failedEvent.Id, warning.Properties["ScheduledNotificationId"]);
        Assert.Equal(fixture.Task.Id, warning.Properties["TaskId"]);
        Assert.Equal(ScheduledNotificationType.DueSoon.ToString(), warning.Properties["NotificationType"]);
        Assert.Equal(1, warning.Properties["RetryCount"]);
        Assert.Equal(true, warning.Properties["RetryStatePersisted"]);

        var notifications = new TestNotificationService();
        var retryService = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);
        var retried = await retryService.ProcessDueAsync();

        Assert.Equal(1, retried.EventsSent);
        Assert.Single(notifications.Sent);
        var sentEvent = await context.ScheduledNotifications.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduledNotificationStatus.Sent, sentEvent.Status);
        Assert.Equal(1, sentEvent.RetryCount);
        Assert.Equal(fixture.Task.Id, sentEvent.TaskId);
    }

    [Fact]
    public async Task InactiveProjectPolicy_OverridesActiveGlobalPolicy()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        context.ReminderPolicies.Add(new ReminderPolicy
        {
            Id = Guid.NewGuid(),
            ScopeType = ReminderPolicyScope.Project,
            ProjectId = fixture.Project.Id,
            BeforeDueHours = 24,
            OverdueEscalationHours = 24,
            NotifyAssignee = true,
            NotifyManager = true,
            IsActive = false,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        });
        await context.SaveChangesAsync();
        var notifications = new TestNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications);

        var result = await service.ProcessDueAsync();

        Assert.Equal(1, result.EventsSuppressed);
        Assert.Empty(notifications.Sent);
        Assert.Equal(
            ScheduledNotificationStatus.Suppressed,
            (await context.ScheduledNotifications.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task GetTaskReminders_UsesExistingTaskAuthorizationScope()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other history unit" };
        context.Units.Add(otherUnit);
        await context.SaveChangesAsync();
        var otherManager = await DeadlineReminderTestData.AddManagerAsync(
            context,
            otherUnit.Id,
            "Other History Manager");
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            new TestNotificationService());
        await service.ProcessDueAsync();

        var ownHistory = await service.GetTaskRemindersAsync(
            fixture.Task.Id,
            fixture.Employee.Id);

        Assert.Single(ownHistory);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetTaskRemindersAsync(
            fixture.Task.Id,
            otherManager.Id));
    }

    [Fact]
    public async Task RetryLimit_PreventsFurtherDeliveryAttempts()
    {
        await using var context = TestFactory.CreateDbContext();
        await DeadlineReminderTestData.SeedAsync(
            context,
            Now.UtcDateTime.AddHours(24));
        var notifications = new FailingNotificationService();
        var service = TestFactory.CreateDeadlineReminderService(
            context,
            new FixedTimeProvider(Now),
            notifications,
            options: new DeadlineReminderOptions { MaxRetryCount = 1 });

        var first = await service.ProcessDueAsync();
        var second = await service.ProcessDueAsync();

        Assert.Equal(1, first.Failures);
        Assert.Equal(0, second.Failures);
        Assert.Equal(1, notifications.Attempts);
        var scheduled = await context.ScheduledNotifications.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduledNotificationStatus.Failed, scheduled.Status);
        Assert.Equal(1, scheduled.RetryCount);
    }

    private sealed class FailingNotificationService : INotificationService
    {
        public int Attempts { get; private set; }

        public Task AddNotification(
            Guid userId,
            string message,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Simulated transient inbox failure.");
        }

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
