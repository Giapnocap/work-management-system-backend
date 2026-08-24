using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services;

public sealed class DeadlineReminderService : IDeadlineReminderService
{
    private const int LastErrorMaxLength = 1000;

    private readonly IAppDbContext _context;
    private readonly ITaskAccessService _taskAccessService;
    private readonly INotificationService _notificationService;
    private readonly ITransactionManager _transactionManager;
    private readonly IAuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly DeadlineReminderOptions _options;
    private readonly ILogger<DeadlineReminderService> _logger;

    public DeadlineReminderService(
        IAppDbContext context,
        ITaskAccessService taskAccessService,
        INotificationService notificationService,
        ITransactionManager transactionManager,
        IAuditService auditService,
        TimeProvider timeProvider,
        IOptions<DeadlineReminderOptions> options,
        ILogger<DeadlineReminderService> logger)
    {
        _context = context;
        _taskAccessService = taskAccessService;
        _notificationService = notificationService;
        _transactionManager = transactionManager;
        _auditService = auditService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeadlineReminderProcessingResult> ProcessDueAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return new DeadlineReminderProcessingResult(0, 0, 0, 0, 0);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var staged = await StageDueEventsAsync(nowUtc, cancellationToken);
        var eventIds = await _context.ScheduledNotifications
            .AsNoTracking()
            .Where(notification =>
                notification.ScheduledForUtc <= nowUtc &&
                notification.RetryCount < _options.MaxRetryCount &&
                (notification.Status == ScheduledNotificationStatus.Pending ||
                 notification.Status == ScheduledNotificationStatus.Failed))
            .OrderBy(notification => notification.ScheduledForUtc)
            .ThenBy(notification => notification.Id)
            .Select(notification => notification.Id)
            .Take(_options.MaxDeliveriesPerBatch)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var suppressed = staged.EventsSuppressed;
        var failures = 0;
        foreach (var eventId in eventIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await DeliverAsync(eventId, nowUtc, cancellationToken);
            switch (outcome)
            {
                case DeliveryOutcome.Sent:
                    sent++;
                    break;
                case DeliveryOutcome.Suppressed:
                    suppressed++;
                    break;
                case DeliveryOutcome.Failed:
                    failures++;
                    break;
            }
        }

        return new DeadlineReminderProcessingResult(
            staged.TasksEvaluated,
            staged.EventsCreated,
            sent,
            suppressed,
            failures);
    }

    public async Task<List<ScheduledNotificationDto>> GetTaskRemindersAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var taskExists = await _context.Tasks
            .AsNoTracking()
            .AnyAsync(task => task.Id == taskId, cancellationToken);
        if (!taskExists)
            throw new NotFoundException("Không tìm thấy công việc.");

        if (!await _taskAccessService.CanAccessTask(
                taskId,
                requesterId,
                cancellationToken: cancellationToken))
        {
            throw new ForbiddenException("Bạn không có quyền xem lịch sử nhắc hạn của công việc này.");
        }

        return await _context.ScheduledNotifications
            .AsNoTracking()
            .Where(notification => notification.TaskId == taskId)
            .OrderByDescending(notification => notification.ScheduledForUtc)
            .ThenByDescending(notification => notification.Id)
            .Select(notification => new ScheduledNotificationDto
            {
                Id = notification.Id,
                TaskId = notification.TaskId,
                Type = notification.Type.ToString(),
                Status = notification.Status.ToString(),
                ScheduledForUtc = notification.ScheduledForUtc,
                CreatedAtUtc = notification.CreatedAtUtc,
                SentAtUtc = notification.SentAtUtc,
                RetryCount = notification.RetryCount,
                LastError = notification.LastError
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<StagingResult> StageDueEventsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var attemptedEventKeys = new List<string>();
        try
        {
            return await _transactionManager.ExecuteAsync(
                async token =>
                {
                    var candidateDeadline = nowUtc.AddHours(DeadlineReminderOptions.MaxPolicyHours);
                    var tasks = await _context.Tasks
                        .AsNoTracking()
                        .Where(task =>
                            task.DueDate.HasValue &&
                            task.DueDate.Value <= candidateDeadline &&
                            task.Status != TaskStatusEnum.Approved &&
                            (!_context.ScheduledNotifications.Any(notification =>
                                 notification.TaskId == task.Id &&
                                 notification.Type == ScheduledNotificationType.DueSoon) ||
                             !_context.ScheduledNotifications.Any(notification =>
                                 notification.TaskId == task.Id &&
                                 notification.Type == ScheduledNotificationType.OverdueAssignee) ||
                             !_context.ScheduledNotifications.Any(notification =>
                                 notification.TaskId == task.Id &&
                                 notification.Type == ScheduledNotificationType.ManagerEscalation)))
                        .OrderBy(task => task.DueDate)
                        .ThenBy(task => task.Id)
                        .Select(task => new DeadlineTask(
                            task.Id,
                            task.UnitId,
                            task.ProjectId,
                            task.DueDate!.Value))
                        .Take(_options.MaxTasksPerBatch)
                        .ToListAsync(token);
                    if (tasks.Count == 0)
                        return new StagingResult(0, 0, 0);

                    var taskIds = tasks.Select(task => task.Id).ToList();
                    var unitIds = tasks
                        .Where(task => task.UnitId.HasValue)
                        .Select(task => task.UnitId!.Value)
                        .Distinct()
                        .ToList();
                    var projectIds = tasks
                        .Where(task => task.ProjectId.HasValue)
                        .Select(task => task.ProjectId!.Value)
                        .Distinct()
                        .ToList();
                    var policies = await _context.ReminderPolicies
                        .AsNoTracking()
                        .Where(policy =>
                            policy.ScopeType == ReminderPolicyScope.Global ||
                            (policy.UnitId.HasValue && unitIds.Contains(policy.UnitId.Value)) ||
                            (policy.ProjectId.HasValue && projectIds.Contains(policy.ProjectId.Value)))
                        .ToListAsync(token);
                    var existingEvents = await _context.ScheduledNotifications
                        .AsNoTracking()
                        .Where(notification => taskIds.Contains(notification.TaskId))
                        .Select(notification => new { notification.TaskId, notification.Type })
                        .ToListAsync(token);
                    var existingTypes = existingEvents
                        .GroupBy(notification => notification.TaskId)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(notification => notification.Type).ToHashSet());

                    var created = 0;
                    var suppressed = 0;
                    foreach (var task in tasks)
                    {
                        var policy = ResolveEffectivePolicy(task, policies);
                        if (policy == null)
                            continue;

                        if (!existingTypes.TryGetValue(task.Id, out var taskEventTypes))
                        {
                            taskEventTypes = new HashSet<ScheduledNotificationType>();
                            existingTypes[task.Id] = taskEventTypes;
                        }

                        var dueDateUtc = DateTime.SpecifyKind(task.DueDateUtc, DateTimeKind.Utc);
                        var dueSoonAtUtc = dueDateUtc.AddHours(-policy.BeforeDueHours);
                        if (nowUtc >= dueSoonAtUtc &&
                            taskEventTypes.Add(ScheduledNotificationType.DueSoon))
                        {
                            var enabled = policy.IsActive && policy.NotifyAssignee && nowUtc < dueDateUtc;
                            var reason = nowUtc >= dueDateUtc
                                ? "Mốc nhắc trước hạn đã qua."
                                : GetPolicySuppressionReason(policy, notifyEnabled: policy.NotifyAssignee);
                            await AddEventAsync(
                                task.Id,
                                ScheduledNotificationType.DueSoon,
                                dueSoonAtUtc,
                                enabled,
                                reason,
                                nowUtc,
                                attemptedEventKeys,
                                token);
                            created++;
                            if (!enabled)
                                suppressed++;
                        }

                        if (nowUtc >= dueDateUtc &&
                            taskEventTypes.Add(ScheduledNotificationType.OverdueAssignee))
                        {
                            var enabled = policy.IsActive && policy.NotifyAssignee;
                            await AddEventAsync(
                                task.Id,
                                ScheduledNotificationType.OverdueAssignee,
                                dueDateUtc,
                                enabled,
                                GetPolicySuppressionReason(policy, policy.NotifyAssignee),
                                nowUtc,
                                attemptedEventKeys,
                                token);
                            created++;
                            if (!enabled)
                                suppressed++;
                        }

                        var escalationAtUtc = dueDateUtc.AddHours(policy.OverdueEscalationHours);
                        if (nowUtc >= escalationAtUtc &&
                            taskEventTypes.Add(ScheduledNotificationType.ManagerEscalation))
                        {
                            var enabled = policy.IsActive && policy.NotifyManager;
                            await AddEventAsync(
                                task.Id,
                                ScheduledNotificationType.ManagerEscalation,
                                escalationAtUtc,
                                enabled,
                                GetPolicySuppressionReason(policy, policy.NotifyManager),
                                nowUtc,
                                attemptedEventKeys,
                                token);
                            created++;
                            if (!enabled)
                                suppressed++;
                        }
                    }

                    if (created > 0)
                        await _context.SaveChangesAsync(token);

                    return new StagingResult(tasks.Count, created, suppressed);
                },
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await AllAttemptedEventsExistAsync(attemptedEventKeys, cancellationToken))
            {
                _logger.LogInformation(
                    "Another deadline worker persisted the same reminder milestones first.");
                return new StagingResult(0, 0, 0);
            }

            throw;
        }
    }

    private async Task AddEventAsync(
        Guid taskId,
        ScheduledNotificationType type,
        DateTime scheduledForUtc,
        bool enabled,
        string? suppressionReason,
        DateTime nowUtc,
        ICollection<string> attemptedEventKeys,
        CancellationToken cancellationToken)
    {
        var eventKey = BuildEventKey(taskId, type);
        attemptedEventKeys.Add(eventKey);
        var status = enabled
            ? ScheduledNotificationStatus.Pending
            : ScheduledNotificationStatus.Suppressed;
        var notification = new ScheduledNotification
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Type = type,
            ScheduledForUtc = scheduledForUtc,
            CreatedAtUtc = nowUtc,
            Status = status,
            EventKey = eventKey,
            LastError = enabled ? null : suppressionReason
        };
        _context.ScheduledNotifications.Add(notification);

        if (!enabled)
        {
            await _auditService.RecordAsync(
                AuditEntityTypes.DeadlineNotification,
                notification.Id,
                AuditActions.Suppressed,
                null,
                new { taskId, Type = type.ToString(), scheduledForUtc, suppressionReason },
                cancellationToken);
        }
    }

    private async Task<DeliveryOutcome> DeliverAsync(
        Guid eventId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _transactionManager.ExecuteAsync(
                async token =>
                {
                    var scheduled = await _context.ScheduledNotifications
                        .FirstOrDefaultAsync(notification => notification.Id == eventId, token);
                    if (scheduled == null ||
                        scheduled.Status is ScheduledNotificationStatus.Sent or
                            ScheduledNotificationStatus.Suppressed ||
                        scheduled.RetryCount >= _options.MaxRetryCount ||
                        scheduled.ScheduledForUtc > nowUtc)
                    {
                        return DeliveryOutcome.Skipped;
                    }

                    var task = await _context.Tasks
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(candidate => candidate.Id == scheduled.TaskId)
                        .Select(candidate => new DeliveryTask(
                            candidate.Id,
                            candidate.Title,
                            candidate.UnitId,
                            candidate.ProjectId,
                            candidate.DueDate,
                            candidate.Status,
                            candidate.IsDeleted))
                        .FirstOrDefaultAsync(token);
                    if (task == null || task.IsDeleted || task.Status == TaskStatusEnum.Approved)
                    {
                        await SuppressAsync(
                            scheduled,
                            task == null ? "Công việc không còn tồn tại." : "Công việc đã hoàn thành hoặc bị xóa.",
                            token);
                        await _context.SaveChangesAsync(token);
                        return DeliveryOutcome.Suppressed;
                    }

                    if (!task.DueDateUtc.HasValue)
                    {
                        await SuppressAsync(scheduled, "Công việc không còn deadline.", token);
                        await _context.SaveChangesAsync(token);
                        return DeliveryOutcome.Suppressed;
                    }

                    var policy = await LoadEffectivePolicyAsync(task, token);
                    var timing = ResolveCurrentTiming(task.DueDateUtc.Value, scheduled.Type, policy);
                    if (!timing.Enabled)
                    {
                        await SuppressAsync(
                            scheduled,
                            timing.Reason ?? "Chính sách nhắc hạn không còn cho phép gửi mốc này.",
                            token);
                        await _context.SaveChangesAsync(token);
                        return DeliveryOutcome.Suppressed;
                    }
                    if (timing.ScheduledForUtc > nowUtc)
                    {
                        scheduled.ScheduledForUtc = timing.ScheduledForUtc;
                        scheduled.Status = ScheduledNotificationStatus.Pending;
                        scheduled.LastError = null;
                        await _context.SaveChangesAsync(token);
                        return DeliveryOutcome.Deferred;
                    }

                    var recipients = scheduled.Type == ScheduledNotificationType.ManagerEscalation
                        ? await GetManagerRecipientsAsync(task.UnitId, token)
                        : await GetAssigneeRecipientsAsync(task.Id, task.UnitId, token);
                    if (recipients.Count == 0)
                        throw new ReminderRecipientUnavailableException(GetMissingRecipientMessage(scheduled.Type));

                    var message = BuildMessage(task.Title, task.DueDateUtc.Value, scheduled.Type);
                    foreach (var recipientId in recipients)
                    {
                        await _notificationService.AddNotification(
                            recipientId,
                            message,
                            token);
                    }

                    scheduled.Status = ScheduledNotificationStatus.Sent;
                    scheduled.SentAtUtc = nowUtc;
                    scheduled.LastError = null;
                    await _auditService.RecordAsync(
                        AuditEntityTypes.DeadlineNotification,
                        scheduled.Id,
                        AuditActions.Sent,
                        null,
                        new
                        {
                            scheduled.TaskId,
                            Type = scheduled.Type.ToString(),
                            RecipientCount = recipients.Count,
                            scheduled.ScheduledForUtc,
                            SentAtUtc = nowUtc
                        },
                        token);
                    await _context.SaveChangesAsync(token);
                    return DeliveryOutcome.Sent;
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (await EventWasFinalizedAsync(eventId, cancellationToken))
                return DeliveryOutcome.Skipped;

            var failure = await RecordFailureAsync(
                eventId,
                new InvalidOperationException("Deadline notification concurrency conflict."),
                cancellationToken);
            LogDeliveryFailure(exception, eventId, failure);
            return failure.Persisted ? DeliveryOutcome.Failed : DeliveryOutcome.Skipped;
        }
        catch (Exception exception)
        {
            var failure = await RecordFailureAsync(eventId, exception, cancellationToken);
            LogDeliveryFailure(exception, eventId, failure);
            return DeliveryOutcome.Failed;
        }
    }

    private async Task<List<Guid>> GetAssigneeRecipientsAsync(
        Guid taskId,
        Guid? taskUnitId,
        CancellationToken cancellationToken)
    {
        var directRecipients = await (
                from assignee in _context.TaskAssignees.AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on assignee.UserId equals (Guid?)user.Id
                where assignee.TaskId == taskId &&
                      assignee.UserId.HasValue &&
                      user.Role == SystemRoles.User &&
                      user.IsApproved &&
                      !user.IsDeleted &&
                      user.UnitId == taskUnitId
                select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (directRecipients.Count > 0)
            return directRecipients;

        var hasUnitAssignment = await _context.TaskAssignees
            .AsNoTracking()
            .AnyAsync(assignee =>
                assignee.TaskId == taskId &&
                assignee.UnitId.HasValue &&
                assignee.UnitId == taskUnitId,
                cancellationToken);
        if (!hasUnitAssignment || !taskUnitId.HasValue)
            return new List<Guid>();

        return await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.UnitId == taskUnitId &&
                user.Role == SystemRoles.User &&
                user.IsApproved &&
                !user.IsDeleted)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    private Task<List<Guid>> GetManagerRecipientsAsync(
        Guid? taskUnitId,
        CancellationToken cancellationToken)
    {
        if (!taskUnitId.HasValue)
            return Task.FromResult(new List<Guid>());

        return _context.Users
            .AsNoTracking()
            .Where(user =>
                user.UnitId == taskUnitId.Value &&
                user.Role == SystemRoles.Manager &&
                user.IsApproved &&
                !user.IsDeleted)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<ReminderPolicy?> LoadEffectivePolicyAsync(
        DeliveryTask task,
        CancellationToken cancellationToken)
    {
        return await _context.ReminderPolicies
            .AsNoTracking()
            .Where(policy =>
                policy.ScopeType == ReminderPolicyScope.Global ||
                (policy.ScopeType == ReminderPolicyScope.Unit && policy.UnitId == task.UnitId) ||
                (policy.ScopeType == ReminderPolicyScope.Project && policy.ProjectId == task.ProjectId))
            .OrderByDescending(policy => policy.ScopeType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ReminderPolicy? ResolveEffectivePolicy(
        DeadlineTask task,
        IReadOnlyCollection<ReminderPolicy> policies)
    {
        if (task.ProjectId.HasValue)
        {
            var projectPolicy = policies.FirstOrDefault(policy =>
                policy.ScopeType == ReminderPolicyScope.Project &&
                policy.ProjectId == task.ProjectId);
            if (projectPolicy != null)
                return projectPolicy;
        }

        if (task.UnitId.HasValue)
        {
            var unitPolicy = policies.FirstOrDefault(policy =>
                policy.ScopeType == ReminderPolicyScope.Unit &&
                policy.UnitId == task.UnitId);
            if (unitPolicy != null)
                return unitPolicy;
        }

        return policies.FirstOrDefault(policy => policy.ScopeType == ReminderPolicyScope.Global);
    }

    private static ReminderTiming ResolveCurrentTiming(
        DateTime dueDate,
        ScheduledNotificationType type,
        ReminderPolicy? policy)
    {
        if (policy == null)
            return new ReminderTiming(false, dueDate, "Không còn chính sách nhắc hạn phù hợp.");

        var dueDateUtc = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        var scheduledForUtc = type switch
        {
            ScheduledNotificationType.DueSoon => dueDateUtc.AddHours(-policy.BeforeDueHours),
            ScheduledNotificationType.OverdueAssignee => dueDateUtc,
            ScheduledNotificationType.ManagerEscalation => dueDateUtc.AddHours(policy.OverdueEscalationHours),
            _ => dueDateUtc
        };
        var targetEnabled = type == ScheduledNotificationType.ManagerEscalation
            ? policy.NotifyManager
            : policy.NotifyAssignee;
        var enabled = policy.IsActive && targetEnabled;
        return new ReminderTiming(
            enabled,
            scheduledForUtc,
            GetPolicySuppressionReason(policy, targetEnabled));
    }

    private async Task SuppressAsync(
        ScheduledNotification scheduled,
        string reason,
        CancellationToken cancellationToken)
    {
        scheduled.Status = ScheduledNotificationStatus.Suppressed;
        scheduled.LastError = Truncate(reason);
        await _auditService.RecordAsync(
            AuditEntityTypes.DeadlineNotification,
            scheduled.Id,
            AuditActions.Suppressed,
            null,
            new { scheduled.TaskId, Type = scheduled.Type.ToString(), Reason = reason },
            cancellationToken);
    }

    private async Task<FailureRecordResult> RecordFailureAsync(
        Guid eventId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Guid? taskId = null;
        ScheduledNotificationType? notificationType = null;
        int? retryCount = null;
        try
        {
            return await _transactionManager.ExecuteAsync(
                async token =>
                {
                    var scheduled = await _context.ScheduledNotifications
                        .FirstOrDefaultAsync(notification => notification.Id == eventId, token);
                    if (scheduled == null ||
                        scheduled.Status is ScheduledNotificationStatus.Sent or
                            ScheduledNotificationStatus.Suppressed)
                    {
                        return new FailureRecordResult(
                            false,
                            scheduled?.TaskId,
                            scheduled?.Type,
                            scheduled?.RetryCount);
                    }

                    taskId = scheduled.TaskId;
                    notificationType = scheduled.Type;
                    scheduled.Status = ScheduledNotificationStatus.Failed;
                    scheduled.RetryCount++;
                    retryCount = scheduled.RetryCount;
                    scheduled.LastError = Truncate(GetSafeFailureMessage(exception));
                    await _context.SaveChangesAsync(token);
                    return new FailureRecordResult(
                        true,
                        taskId,
                        notificationType,
                        retryCount);
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception recordException)
        {
            _logger.LogError(
                recordException,
                "Could not persist failure state for deadline notification {ScheduledNotificationId} on task {TaskId} ({NotificationType}) at retry {RetryCount}.",
                eventId,
                taskId,
                notificationType?.ToString(),
                retryCount);
            return new FailureRecordResult(
                false,
                taskId,
                notificationType,
                retryCount);
        }
    }

    private void LogDeliveryFailure(
        Exception exception,
        Guid eventId,
        FailureRecordResult failure)
    {
        _logger.LogWarning(
            exception,
            "Deadline notification {ScheduledNotificationId} on task {TaskId} ({NotificationType}) failed at retry {RetryCount}; retry state persisted: {RetryStatePersisted}.",
            eventId,
            failure.TaskId,
            failure.NotificationType?.ToString(),
            failure.RetryCount,
            failure.Persisted);
    }

    private async Task<bool> AllAttemptedEventsExistAsync(
        IReadOnlyCollection<string> eventKeys,
        CancellationToken cancellationToken)
    {
        if (eventKeys.Count == 0)
            return false;

        var distinctKeys = eventKeys.Distinct().ToList();
        var persistedCount = await _context.ScheduledNotifications
            .AsNoTracking()
            .CountAsync(notification => distinctKeys.Contains(notification.EventKey), cancellationToken);
        return persistedCount == distinctKeys.Count;
    }

    private Task<bool> EventWasFinalizedAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return _context.ScheduledNotifications
            .AsNoTracking()
            .AnyAsync(notification =>
                notification.Id == eventId &&
                (notification.Status == ScheduledNotificationStatus.Sent ||
                 notification.Status == ScheduledNotificationStatus.Suppressed),
                cancellationToken);
    }

    private static string BuildEventKey(Guid taskId, ScheduledNotificationType type)
        => $"deadline:{taskId:N}:{type}";

    private static string BuildMessage(
        string taskTitle,
        DateTime dueDate,
        ScheduledNotificationType type)
    {
        var dueDateUtc = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        return type switch
        {
            ScheduledNotificationType.DueSoon =>
                $"Công việc '{taskTitle}' sẽ đến hạn lúc {dueDateUtc:dd/MM/yyyy HH:mm} UTC.",
            ScheduledNotificationType.OverdueAssignee =>
                $"Công việc '{taskTitle}' đã quá hạn. Vui lòng cập nhật tiến độ.",
            ScheduledNotificationType.ManagerEscalation =>
                $"Công việc '{taskTitle}' của phòng ban đã quá hạn và cần được xử lý.",
            _ => throw new InvalidOperationException("Loại thông báo deadline không hợp lệ.")
        };
    }

    private static string GetMissingRecipientMessage(ScheduledNotificationType type)
    {
        return type == ScheduledNotificationType.ManagerEscalation
            ? "Chưa có Trưởng phòng hợp lệ trong phạm vi quản lý của công việc."
            : "Chưa có nhân viên hợp lệ được giao công việc.";
    }

    private static string? GetPolicySuppressionReason(
        ReminderPolicy policy,
        bool notifyEnabled)
    {
        if (!policy.IsActive)
            return "Chính sách nhắc hạn đang tạm dừng.";
        if (!notifyEnabled)
            return "Loại người nhận này đã bị tắt trong chính sách nhắc hạn.";
        return null;
    }

    private static string GetSafeFailureMessage(Exception exception)
    {
        return exception is ReminderRecipientUnavailableException
            ? exception.Message
            : "Không thể ghi thông báo vào hộp thư. Hệ thống sẽ thử lại.";
    }

    private static string Truncate(string value)
        => value.Length <= LastErrorMaxLength ? value : value[..LastErrorMaxLength];

    private sealed record DeadlineTask(
        Guid Id,
        Guid? UnitId,
        Guid? ProjectId,
        DateTime DueDateUtc);

    private sealed record DeliveryTask(
        Guid Id,
        string Title,
        Guid? UnitId,
        Guid? ProjectId,
        DateTime? DueDateUtc,
        TaskStatusEnum Status,
        bool IsDeleted);

    private sealed record StagingResult(
        int TasksEvaluated,
        int EventsCreated,
        int EventsSuppressed);

    private sealed record ReminderTiming(
        bool Enabled,
        DateTime ScheduledForUtc,
        string? Reason);

    private sealed record FailureRecordResult(
        bool Persisted,
        Guid? TaskId,
        ScheduledNotificationType? NotificationType,
        int? RetryCount);

    private enum DeliveryOutcome
    {
        Skipped,
        Deferred,
        Sent,
        Suppressed,
        Failed
    }

    private sealed class ReminderRecipientUnavailableException : Exception
    {
        public ReminderRecipientUnavailableException(string message) : base(message)
        {
        }
    }
}
