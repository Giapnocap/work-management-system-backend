using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public sealed class TaskTimelineServiceTests
{
    [Fact]
    public async Task GetTaskTimeline_CombinesLifecycleEventsInDeterministicOrder()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedTimelineAsync(context);
        var service = TestFactory.CreateTaskTimelineService(context);

        var result = await service.GetTaskTimelineAsync(
            fixture.Task.Id,
            fixture.Employee.Id,
            new TaskTimelineQueryDto { Size = 50 });

        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.TaskCreated);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.TaskUpdated);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.TaskStatusChanged);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.AssignmentAdded);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.ProgressReported);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.ProgressStatusChanged);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.ReviewCompleted);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.CommentAdded);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.FileUploaded);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.ReminderSent);
        Assert.Contains(result.Items, item => item.Type == TimelineEventTypes.EscalationSent);
        Assert.Equal(
            result.Items.OrderByDescending(item => item.OccurredAtUtc).Select(item => item.Id),
            result.Items.Select(item => item.Id));

        var progress = Assert.Single(result.Items, item => item.Type == TimelineEventTypes.ProgressReported);
        Assert.Equal("Submitted", progress.Metadata?.Status);
        Assert.Equal(80, progress.Metadata?.Percent);
        var progressTransition = Assert.Single(
            result.Items,
            item => item.Type == TimelineEventTypes.ProgressStatusChanged);
        Assert.Equal(progress.RelatedEntityId, progressTransition.RelatedEntityId);
        Assert.Equal("Submitted for review.", progressTransition.Description);
        Assert.Equal("Submitted for review.", progressTransition.Metadata?.Reason);
        Assert.Equal("Submitted", progressTransition.Metadata?.Status);
        var assignment = Assert.Single(result.Items, item => item.Type == TimelineEventTypes.AssignmentAdded);
        Assert.Equal(fixture.Employee.Id, assignment.Metadata?.AssignmentTargetId);
        Assert.Equal(fixture.Employee.FullName, assignment.Metadata?.AssignmentTargetName);
    }

    [Fact]
    public async Task GetTaskTimeline_CursorDoesNotDuplicateOrSkipItemsWithSameTimestamp()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedTimelineAsync(context);
        var occurredAt = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        var commentIds = Enumerable.Range(1, 9)
            .Select(index => new Guid(index, 0, 0, new byte[8]))
            .ToList();
        context.TaskComments.AddRange(commentIds.Select((id, index) => new TaskComment
        {
            Id = id,
            TaskId = fixture.Task.Id,
            UserId = fixture.Employee.Id,
            Content = $"Paged comment {index}",
            CreatedAt = occurredAt
        }));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskTimelineService(context);
        var collectedIds = new List<Guid>();
        string? cursor = null;

        do
        {
            var page = await service.GetTaskTimelineAsync(
                fixture.Task.Id,
                fixture.Employee.Id,
                new TaskTimelineQueryDto
                {
                    Size = 3,
                    Type = TimelineEventTypes.CommentAdded,
                    Cursor = cursor
                });
            collectedIds.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
        }
        while (cursor != null);

        Assert.Equal(commentIds.Count + 1, collectedIds.Count);
        Assert.Equal(collectedIds.Count, collectedIds.Distinct().Count());
        Assert.All(commentIds, id => Assert.Contains(id, collectedIds));
    }

    [Fact]
    public async Task GetTaskTimeline_UsesDeletedActorSnapshotAndSafeMetadata()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedTimelineAsync(context);
        var deletedActor = CreateUser("deleted-actor", SystemRoles.User, fixture.Unit.Id);
        deletedActor.FullName = "Former Employee";
        deletedActor.IsDeleted = true;
        context.Users.Add(deletedActor);
        context.TaskHistories.Add(new TaskHistory
        {
            Id = Guid.NewGuid(),
            TaskId = fixture.Task.Id,
            ChangedBy = deletedActor.Id,
            FieldName = "Title",
            OldValue = "Old title",
            NewValue = "Updated title",
            ChangedAt = DateTime.UtcNow.AddMinutes(1)
        });
        context.TaskHistories.Add(new TaskHistory
        {
            Id = Guid.NewGuid(),
            TaskId = fixture.Task.Id,
            ChangedBy = deletedActor.Id,
            FieldName = "PasswordHash",
            NewValue = "must-not-leak",
            ChangedAt = DateTime.UtcNow.AddMinutes(2)
        });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskTimelineService(context);

        var result = await service.GetTaskTimelineAsync(
            fixture.Task.Id,
            fixture.Manager.Id,
            new TaskTimelineQueryDto
            {
                Type = TimelineEventTypes.TaskUpdated,
                ActorId = deletedActor.Id
            });

        var item = Assert.Single(result.Items);
        Assert.Equal("Former Employee", item.Actor.DisplayName);
        Assert.True(item.Actor.IsDeleted);
        Assert.Equal("Title", item.Metadata?.FieldName);
        Assert.DoesNotContain(result.Items, candidate => candidate.Metadata?.FieldName == "PasswordHash");
    }

    [Fact]
    public async Task GetTaskTimeline_FiltersByActorAndUtcDateRange()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedTimelineAsync(context);
        var service = TestFactory.CreateTaskTimelineService(context);
        var fromUtc = new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);

        var result = await service.GetTaskTimelineAsync(
            fixture.Task.Id,
            fixture.Employee.Id,
            new TaskTimelineQueryDto
            {
                ActorId = fixture.Employee.Id,
                FromUtc = fromUtc,
                ToUtc = toUtc
            });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(fixture.Employee.Id, item.Actor.Id);
            Assert.InRange(item.OccurredAtUtc, fromUtc.UtcDateTime, toUtc.UtcDateTime);
        });
    }

    [Fact]
    public async Task GetTaskTimeline_RejectsCrossUnitAccessAndInvalidQueryValues()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedTimelineAsync(context);
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other Unit" };
        var outsideManager = CreateUser("outside-manager", SystemRoles.Manager, otherUnit.Id);
        context.AddRange(otherUnit, outsideManager);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskTimelineService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetTaskTimelineAsync(
            fixture.Task.Id,
            outsideManager.Id,
            new TaskTimelineQueryDto()));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetTaskTimelineAsync(
            fixture.Task.Id,
            fixture.Manager.Id,
            new TaskTimelineQueryDto { Cursor = "invalid" }));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetTaskTimelineAsync(
            fixture.Task.Id,
            fixture.Manager.Id,
            new TaskTimelineQueryDto
            {
                FromUtc = DateTimeOffset.UtcNow,
                ToUtc = DateTimeOffset.UtcNow.AddDays(-1)
            }));
    }

    private static async Task<TimelineFixture> SeedTimelineAsync(AppDbContext context)
    {
        var start = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Timeline Unit" };
        var manager = CreateUser("timeline-manager", SystemRoles.Manager, unit.Id);
        var employee = CreateUser("timeline-employee", SystemRoles.User, unit.Id);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Timeline task",
            Description = "Timeline test",
            CreatedBy = manager.Id,
            CreatedAt = start,
            UnitId = unit.Id,
            Status = TaskStatusEnum.Approved,
            CompletedAt = start.AddHours(8),
            CompletedBy = employee.Id
        };
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id,
            Percent = 80,
            Description = "Implementation ready",
            Status = ProgressStatus.Submitted,
            HoursSpent = 3,
            UpdatedAt = start.AddHours(3)
        };

        context.AddRange(unit, manager, employee, task, progress);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id
        });
        context.TaskHistories.AddRange(
            new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = manager.Id,
                FieldName = "Created",
                NewValue = task.Title,
                ChangedAt = start
            },
            new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = manager.Id,
                FieldName = "Priority",
                OldValue = "Medium",
                NewValue = "High",
                ChangedAt = start.AddHours(1)
            },
            new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = manager.Id,
                FieldName = "Remind",
                NewValue = "Manual reminder",
                ChangedAt = start.AddHours(2)
            },
            new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = employee.Id,
                FieldName = "ProgressStatus",
                OldValue = ProgressStatus.InProgress.ToString(),
                NewValue = ProgressStatus.Submitted.ToString(),
                RelatedEntityId = progress.Id,
                Reason = "Submitted for review.",
                ChangedAt = start.AddHours(3).AddMinutes(30)
            });
        context.Reviews.Add(new ReportReview
        {
            Id = Guid.NewGuid(),
            ProgressId = progress.Id,
            ReviewerId = manager.Id,
            IsApproved = true,
            Comment = "Accepted",
            ReviewedAt = start.AddHours(4)
        });
        context.TaskComments.Add(new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id,
            Content = "A collaboration note",
            CreatedAt = start.AddHours(5)
        });
        context.UploadFiles.Add(new UploadFile
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            ProgressId = progress.Id,
            UploadedBy = employee.Id,
            FileName = "evidence.pdf",
            StorageKey = "evidence.pdf",
            CreatedAt = start.AddHours(6)
        });
        context.ScheduledNotifications.Add(new ScheduledNotification
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Type = ScheduledNotificationType.ManagerEscalation,
            Status = ScheduledNotificationStatus.Sent,
            EventKey = $"timeline-{Guid.NewGuid():N}",
            CreatedAtUtc = start,
            ScheduledForUtc = start.AddHours(7),
            SentAtUtc = start.AddHours(7)
        });
        await context.SaveChangesAsync();
        return new TimelineFixture(unit, manager, employee, task);
    }

    private static User CreateUser(string username, string role, Guid? unitId)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = $"EMP{Guid.NewGuid():N}"[..12],
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };

    private sealed record TimelineFixture(Unit Unit, User Manager, User Employee, TaskItem Task);
}
