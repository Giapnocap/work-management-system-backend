using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Services;

public sealed class TaskTimelineService : ITaskTimelineService
{
    private const int HistorySource = 0;
    private const int AssignmentSource = 1;
    private const int ProgressSource = 2;
    private const int ReviewSource = 3;
    private const int CommentSource = 4;
    private const int FileSource = 5;
    private const int NotificationSource = 6;
    private const int CompletionSource = 7;
    private const int MaxTextLength = 500;

    private static readonly string[] SafeHistoryFields =
    {
        "Created",
        "Title",
        "Description",
        "StartDate",
        "DueDate",
        "RequiresReview",
        "PlannedEffortHours",
        "Priority",
        "ProjectId",
        "Status",
        "ProgressStatus",
        "IsDeleted",
        "Remind",
        "DependencyAdded",
        "DependencyRemoved",
        "DependencyUnblocked",
        "RecurringTaskGenerated"
    };

    private readonly IAppDbContext _context;
    private readonly ITaskAccessService _taskAccessService;

    public TaskTimelineService(
        IAppDbContext context,
        ITaskAccessService taskAccessService)
    {
        _context = context;
        _taskAccessService = taskAccessService;
    }

    public async Task<TimelinePageDto> GetTaskTimelineAsync(
        Guid taskId,
        Guid requesterId,
        TaskTimelineQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _taskAccessService.CanAccessTask(taskId, requesterId, cancellationToken: cancellationToken))
            throw new ForbiddenException("Ban khong co quyen xem dong hoat dong cua cong viec nay.");

        var normalizedType = NormalizeType(query.Type);
        var fromUtc = query.FromUtc?.UtcDateTime;
        var toUtc = query.ToUtc?.UtcDateTime;
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new BusinessException("Khoang thoi gian timeline khong hop le.");

        var cursor = DecodeCursor(query.Cursor);
        var size = Math.Clamp(query.Size, 1, Paging.MaxPageSize);
        var sourceLimit = size + 1;
        var candidates = new List<TimelineCandidate>(sourceLimit * 2);

        foreach (var source in BuildSources(taskId))
        {
            if (normalizedType != null && !source.SupportedTypes.Contains(normalizedType))
                continue;

            var sourceItems = await ApplyCommonFilters(
                    source.Query,
                    normalizedType,
                    query.ActorId,
                    fromUtc,
                    toUtc,
                    cursor)
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenBy(item => item.SourceOrder)
                .ThenByDescending(item => item.Id)
                .Take(sourceLimit)
                .ToListAsync(cancellationToken);
            for (var index = 0; index < sourceItems.Count; index++)
                sourceItems[index].SourcePosition = index;
            candidates.AddRange(sourceItems);
        }

        var orderedCandidates = candidates
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.SourceOrder)
            .ThenBy(item => item.SourcePosition)
            .Take(sourceLimit)
            .ToList();
        var hasMore = orderedCandidates.Count > size;
        if (hasMore)
            orderedCandidates.RemoveAt(orderedCandidates.Count - 1);

        var users = await LoadUsersAsync(orderedCandidates, cancellationToken);
        var units = await LoadUnitsAsync(orderedCandidates, cancellationToken);
        var items = orderedCandidates
            .Select(candidate => MapItem(candidate, users, units))
            .ToList();
        var last = orderedCandidates.LastOrDefault();

        return new TimelinePageDto
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore && last != null
                ? EncodeCursor(new TimelineCursor(last.OccurredAtUtc, last.SourceOrder, last.Id))
                : null
        };
    }

    private IReadOnlyList<TimelineSource> BuildSources(Guid taskId)
    {
        var history = _context.TaskHistories
            .AsNoTracking()
            .Where(item => item.TaskId == taskId && SafeHistoryFields.Contains(item.FieldName))
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.ChangedAt,
                SourceOrder = HistorySource,
                Type = item.FieldName == "Created"
                    ? TimelineEventTypes.TaskCreated
                    : item.FieldName == "Status"
                        ? TimelineEventTypes.TaskStatusChanged
                        : item.FieldName == "ProgressStatus"
                            ? TimelineEventTypes.ProgressStatusChanged
                        : item.FieldName == "Remind"
                            ? TimelineEventTypes.ReminderSent
                            : TimelineEventTypes.TaskUpdated,
                ActorId = item.ChangedBy,
                RelatedEntityId = item.RelatedEntityId ?? item.TaskId,
                FieldName = item.FieldName,
                PreviousValue = item.OldValue,
                CurrentValue = item.NewValue,
                Reason = item.Reason,
                Description = item.Reason
            });

        var assignments = _context.TaskAssignees
            .AsNoTracking()
            .Where(item => item.TaskId == taskId)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.Task!.CreatedAt,
                SourceOrder = AssignmentSource,
                Type = TimelineEventTypes.AssignmentAdded,
                ActorId = item.Task.CreatedBy,
                RelatedEntityId = item.Id,
                SubjectUserId = item.UserId,
                SubjectUnitId = item.UnitId
            });

        var progresses = _context.Progresses
            .AsNoTracking()
            .Where(item => item.TaskId == taskId)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.UpdatedAt,
                SourceOrder = ProgressSource,
                Type = TimelineEventTypes.ProgressReported,
                ActorId = item.UserId,
                RelatedEntityId = item.Id,
                Description = item.Description,
                StatusValue = (int)item.Status,
                Percent = item.Percent,
                HoursSpent = item.HoursSpent
            });

        var reviews = _context.Reviews
            .AsNoTracking()
            .Where(item => item.Progress!.TaskId == taskId)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.ReviewedAt,
                SourceOrder = ReviewSource,
                Type = TimelineEventTypes.ReviewCompleted,
                ActorId = item.ReviewerId,
                RelatedEntityId = item.ProgressId,
                Description = item.Comment,
                IsApproved = item.IsApproved
            });

        var comments = _context.TaskComments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TaskId == taskId)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.CreatedAt,
                SourceOrder = CommentSource,
                Type = TimelineEventTypes.CommentAdded,
                ActorId = item.UserId,
                RelatedEntityId = item.Id,
                Description = item.Content,
                IsDeleted = item.IsDeleted
            });

        var files = _context.UploadFiles
            .AsNoTracking()
            .Where(item => item.TaskId == taskId)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.CreatedAt,
                SourceOrder = FileSource,
                Type = TimelineEventTypes.FileUploaded,
                ActorId = item.UploadedBy,
                RelatedEntityId = item.Id,
                FileName = item.FileName
            });

        var notifications = _context.ScheduledNotifications
            .AsNoTracking()
            .Where(item =>
                item.TaskId == taskId &&
                item.Status == ScheduledNotificationStatus.Sent &&
                item.SentAtUtc.HasValue)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.SentAtUtc!.Value,
                SourceOrder = NotificationSource,
                Type = item.Type == ScheduledNotificationType.ManagerEscalation
                    ? TimelineEventTypes.EscalationSent
                    : TimelineEventTypes.ReminderSent,
                RelatedEntityId = item.Id,
                ReminderTypeValue = (int)item.Type
            });

        var completion = _context.Tasks
            .AsNoTracking()
            .Where(item => item.Id == taskId && item.CompletedAt.HasValue)
            .Select(item => new TimelineCandidate
            {
                Id = item.Id,
                OccurredAtUtc = item.CompletedAt!.Value,
                SourceOrder = CompletionSource,
                Type = TimelineEventTypes.TaskStatusChanged,
                ActorId = item.CompletedBy,
                RelatedEntityId = item.Id,
                StatusValue = (int)item.Status
            });

        return new[]
        {
            new TimelineSource(history, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TimelineEventTypes.TaskCreated,
                TimelineEventTypes.TaskUpdated,
                TimelineEventTypes.TaskStatusChanged,
                TimelineEventTypes.ProgressStatusChanged,
                TimelineEventTypes.ReminderSent
            }),
            SingleTypeSource(assignments, TimelineEventTypes.AssignmentAdded),
            SingleTypeSource(progresses, TimelineEventTypes.ProgressReported),
            SingleTypeSource(reviews, TimelineEventTypes.ReviewCompleted),
            SingleTypeSource(comments, TimelineEventTypes.CommentAdded),
            SingleTypeSource(files, TimelineEventTypes.FileUploaded),
            new TimelineSource(notifications, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TimelineEventTypes.ReminderSent,
                TimelineEventTypes.EscalationSent
            }),
            SingleTypeSource(completion, TimelineEventTypes.TaskStatusChanged)
        };
    }

    private static TimelineSource SingleTypeSource(
        IQueryable<TimelineCandidate> query,
        string type)
        => new(query, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { type });

    private static IQueryable<TimelineCandidate> ApplyCommonFilters(
        IQueryable<TimelineCandidate> query,
        string? type,
        Guid? actorId,
        DateTime? fromUtc,
        DateTime? toUtc,
        TimelineCursor? cursor)
    {
        if (type != null)
            query = query.Where(item => item.Type == type);
        if (actorId.HasValue)
            query = query.Where(item => item.ActorId == actorId.Value);
        if (fromUtc.HasValue)
            query = query.Where(item => item.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(item => item.OccurredAtUtc <= toUtc.Value);

        if (cursor.HasValue)
        {
            var value = cursor.Value;
            query = query.Where(item =>
                item.OccurredAtUtc < value.OccurredAtUtc ||
                (item.OccurredAtUtc == value.OccurredAtUtc &&
                 (item.SourceOrder > value.SourceOrder ||
                  (item.SourceOrder == value.SourceOrder && item.Id.CompareTo(value.Id) < 0))));
        }

        return query;
    }

    private async Task<Dictionary<Guid, UserSnapshot>> LoadUsersAsync(
        IReadOnlyCollection<TimelineCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var userIds = candidates
            .SelectMany(item => new[] { item.ActorId, item.SubjectUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserSnapshot>();

        return await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserSnapshot(user.Id, user.FullName, user.EmployeeCode, user.IsDeleted))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadUnitsAsync(
        IReadOnlyCollection<TimelineCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var unitIds = candidates
            .Where(item => item.SubjectUnitId.HasValue)
            .Select(item => item.SubjectUnitId!.Value)
            .Distinct()
            .ToList();
        if (unitIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _context.Units
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(unit => unitIds.Contains(unit.Id))
            .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);
    }

    private static TimelineItemDto MapItem(
        TimelineCandidate candidate,
        IReadOnlyDictionary<Guid, UserSnapshot> users,
        IReadOnlyDictionary<Guid, string> units)
    {
        var target = ResolveAssignmentTarget(candidate, users, units);
        return new TimelineItemDto
        {
            Id = candidate.Id,
            OccurredAtUtc = DateTime.SpecifyKind(candidate.OccurredAtUtc, DateTimeKind.Utc),
            Type = candidate.Type,
            Actor = ResolveActor(candidate.ActorId, users),
            Title = GetTitle(candidate),
            Description = GetDescription(candidate, target.Name),
            RelatedEntityId = candidate.RelatedEntityId,
            Metadata = BuildMetadata(candidate, target)
        };
    }

    private static TimelineActorDto ResolveActor(
        Guid? actorId,
        IReadOnlyDictionary<Guid, UserSnapshot> users)
    {
        if (!actorId.HasValue)
        {
            return new TimelineActorDto
            {
                DisplayName = "System"
            };
        }

        if (!users.TryGetValue(actorId.Value, out var user))
        {
            return new TimelineActorDto
            {
                Id = actorId,
                DisplayName = "Deleted user",
                IsDeleted = true
            };
        }

        return new TimelineActorDto
        {
            Id = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? "Unknown user" : user.FullName,
            EmployeeCode = user.EmployeeCode,
            IsDeleted = user.IsDeleted
        };
    }

    private static AssignmentTarget ResolveAssignmentTarget(
        TimelineCandidate candidate,
        IReadOnlyDictionary<Guid, UserSnapshot> users,
        IReadOnlyDictionary<Guid, string> units)
    {
        if (candidate.SubjectUserId.HasValue)
        {
            var name = users.TryGetValue(candidate.SubjectUserId.Value, out var user)
                ? user.FullName
                : "Deleted user";
            return new AssignmentTarget("User", candidate.SubjectUserId, name);
        }

        if (candidate.SubjectUnitId.HasValue)
        {
            var name = units.TryGetValue(candidate.SubjectUnitId.Value, out var unitName)
                ? unitName
                : "Deleted unit";
            return new AssignmentTarget("Unit", candidate.SubjectUnitId, name);
        }

        return new AssignmentTarget(null, null, null);
    }

    private static string GetTitle(TimelineCandidate candidate)
        => candidate.Type switch
        {
            TimelineEventTypes.TaskCreated => "Task created",
            TimelineEventTypes.TaskUpdated => "Task updated",
            TimelineEventTypes.TaskStatusChanged => "Task status changed",
            TimelineEventTypes.AssignmentAdded => "Assignment added",
            TimelineEventTypes.ProgressReported => "Progress reported",
            TimelineEventTypes.ProgressStatusChanged => "Progress status changed",
            TimelineEventTypes.ReviewCompleted => candidate.IsApproved == true
                ? "Progress approved"
                : "Progress rejected",
            TimelineEventTypes.CommentAdded => candidate.IsDeleted ? "Comment deleted" : "Comment added",
            TimelineEventTypes.FileUploaded => "File uploaded",
            TimelineEventTypes.ReminderSent => "Reminder sent",
            TimelineEventTypes.EscalationSent => "Manager escalation sent",
            _ => "Task activity"
        };

    private static string? GetDescription(TimelineCandidate candidate, string? assignmentTargetName)
    {
        if (candidate.Type == TimelineEventTypes.AssignmentAdded)
            return assignmentTargetName == null ? null : $"Assigned to {assignmentTargetName}.";
        if (candidate.Type == TimelineEventTypes.CommentAdded && candidate.IsDeleted)
            return null;
        if (candidate.Type == TimelineEventTypes.FileUploaded)
            return Limit(candidate.FileName);
        if (candidate.Type is TimelineEventTypes.TaskCreated or TimelineEventTypes.TaskUpdated)
            return Limit(candidate.CurrentValue);

        return Limit(candidate.Description);
    }

    private static TimelineMetadataDto? BuildMetadata(
        TimelineCandidate candidate,
        AssignmentTarget target)
    {
        if (candidate.Type == TimelineEventTypes.TaskCreated)
            return null;

        return new TimelineMetadataDto
        {
            FieldName = candidate.FieldName,
            PreviousValue = Limit(candidate.PreviousValue),
            CurrentValue = Limit(candidate.CurrentValue),
            Reason = Limit(candidate.Reason),
            Status = candidate.Type == TimelineEventTypes.ProgressStatusChanged
                ? Limit(candidate.CurrentValue)
                : ResolveStatus(candidate),
            Percent = candidate.Percent,
            HoursSpent = candidate.HoursSpent,
            IsApproved = candidate.IsApproved,
            FileName = Limit(candidate.FileName),
            AssignmentTargetType = target.Type,
            AssignmentTargetId = target.Id,
            AssignmentTargetName = Limit(target.Name),
            ReminderType = candidate.ReminderTypeValue.HasValue
                ? ((ScheduledNotificationType)candidate.ReminderTypeValue.Value).ToString()
                : null,
            IsDeleted = candidate.IsDeleted
        };
    }

    private static string? ResolveStatus(TimelineCandidate candidate)
    {
        if (!candidate.StatusValue.HasValue)
            return null;

        return candidate.Type == TimelineEventTypes.ProgressReported
            ? ((ProgressStatus)candidate.StatusValue.Value).ToString()
            : ((Domain.Enums.TaskStatus)candidate.StatusValue.Value).ToString();
    }

    private static string? NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        var normalized = type.Trim();
        var knownType = TimelineEventTypes.All.FirstOrDefault(item =>
            string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return knownType ?? throw new BusinessException("Loai su kien timeline khong hop le.");
    }

    private static string? Limit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= MaxTextLength ? trimmed : trimmed[..MaxTextLength];
    }

    private static string EncodeCursor(TimelineCursor cursor)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{cursor.OccurredAtUtc.Ticks}:{cursor.SourceOrder}:{cursor.Id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static TimelineCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        try
        {
            var base64 = cursor.Trim().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
            if (parts.Length != 3 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var sourceOrder) ||
                !Guid.TryParseExact(parts[2], "N", out var id) ||
                sourceOrder is < HistorySource or > CompletionSource)
            {
                throw new FormatException();
            }

            return new TimelineCursor(new DateTime(ticks, DateTimeKind.Utc), sourceOrder, id);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new BusinessException("Cursor timeline khong hop le.");
        }
    }

    private sealed class TimelineCandidate
    {
        public Guid Id { get; init; }
        public DateTime OccurredAtUtc { get; init; }
        public int SourceOrder { get; init; }
        public int SourcePosition { get; set; }
        public string Type { get; init; } = string.Empty;
        public Guid? ActorId { get; init; }
        public Guid RelatedEntityId { get; init; }
        public string? FieldName { get; init; }
        public string? PreviousValue { get; init; }
        public string? CurrentValue { get; init; }
        public string? Reason { get; init; }
        public string? Description { get; init; }
        public int? StatusValue { get; init; }
        public int? Percent { get; init; }
        public decimal? HoursSpent { get; init; }
        public bool? IsApproved { get; init; }
        public string? FileName { get; init; }
        public Guid? SubjectUserId { get; init; }
        public Guid? SubjectUnitId { get; init; }
        public int? ReminderTypeValue { get; init; }
        public bool IsDeleted { get; init; }
    }

    private sealed record TimelineSource(
        IQueryable<TimelineCandidate> Query,
        IReadOnlySet<string> SupportedTypes);

    private sealed record UserSnapshot(Guid Id, string FullName, string EmployeeCode, bool IsDeleted);
    private sealed record AssignmentTarget(string? Type, Guid? Id, string? Name);
    private readonly record struct TimelineCursor(DateTime OccurredAtUtc, int SourceOrder, Guid Id);
}
