using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services;

public sealed class RecurringTaskSchedulerService : IRecurringTaskScheduler
{
    private readonly IAppDbContext _context;
    private readonly ITaskBusinessRuleService _taskRules;
    private readonly IRecurringScheduleCalculator _scheduleCalculator;
    private readonly INotificationService _notificationService;
    private readonly ITransactionManager _transactionManager;
    private readonly IAuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly RecurringTaskOptions _options;
    private readonly ILogger<RecurringTaskSchedulerService> _logger;

    public RecurringTaskSchedulerService(
        IAppDbContext context,
        ITaskBusinessRuleService taskRules,
        IRecurringScheduleCalculator scheduleCalculator,
        INotificationService notificationService,
        ITransactionManager transactionManager,
        IAuditService auditService,
        TimeProvider timeProvider,
        IOptions<RecurringTaskOptions> options,
        ILogger<RecurringTaskSchedulerService> logger)
    {
        _context = context;
        _taskRules = taskRules;
        _scheduleCalculator = scheduleCalculator;
        _notificationService = notificationService;
        _transactionManager = transactionManager;
        _auditService = auditService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RecurringTaskProcessingResult> ProcessDueAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return new RecurringTaskProcessingResult(0, 0, 0);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var dueTemplateIds = await _context.RecurringTaskTemplates
            .AsNoTracking()
            .Where(template => template.IsActive && template.NextRunAtUtc <= nowUtc)
            .OrderBy(template => template.NextRunAtUtc)
            .ThenBy(template => template.Id)
            .Select(template => template.Id)
            .Take(_options.MaxTemplatesPerBatch)
            .ToListAsync(cancellationToken);

        var generatedCount = 0;
        var failureCount = 0;
        foreach (var templateId in dueTemplateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                generatedCount += await ProcessTemplateAsync(
                    templateId,
                    nowUtc,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failureCount++;
                _logger.LogError(
                    exception,
                    "Failed to process recurring task template {TemplateId}; its schedule was not advanced.",
                    templateId);
            }
        }

        return new RecurringTaskProcessingResult(
            dueTemplateIds.Count,
            generatedCount,
            failureCount);
    }

    private async Task<int> ProcessTemplateAsync(
        Guid templateId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        DateTime? attemptedScheduledForUtc = null;
        try
        {
            return await _transactionManager.ExecuteSerializableAsync(
                async token =>
                {
                    var template = await _context.RecurringTaskTemplates
                        .Include(candidate => candidate.Assignees)
                        .FirstOrDefaultAsync(candidate => candidate.Id == templateId, token);
                    if (template == null || !template.IsActive || template.NextRunAtUtc > nowUtc)
                        return 0;

                    if (!template.UnitId.HasValue)
                        throw new BusinessException("Lịch công việc định kỳ không có phòng ban hợp lệ.");

                    await EnsureProjectIsAvailableAsync(template, token);
                    var assignmentPlan = await _taskRules.ResolveAssignmentPlan(
                        template.Assignees.Select(assignee => assignee.UserId),
                        new[] { template.UnitId.Value },
                        template.UnitId.Value,
                        token);
                    var generatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    var generatedCount = 0;
                    var processedOccurrenceCount = 0;

                    while (template.NextRunAtUtc <= nowUtc &&
                           processedOccurrenceCount < _options.MaxCatchUpOccurrencesPerTemplate)
                    {
                        var scheduledForUtc = DateTime.SpecifyKind(
                            template.NextRunAtUtc,
                            DateTimeKind.Utc);
                        attemptedScheduledForUtc = scheduledForUtc;

                        var occurrenceExists = await _context.GeneratedTaskOccurrences
                            .AsNoTracking()
                            .AnyAsync(
                                occurrence => occurrence.TemplateId == template.Id &&
                                              occurrence.ScheduledForUtc == scheduledForUtc,
                                token);
                        if (!occurrenceExists)
                        {
                            await AddGeneratedTaskAsync(
                                template,
                                assignmentPlan,
                                scheduledForUtc,
                                generatedAtUtc,
                                token);
                            generatedCount++;
                        }

                        template.LastGeneratedAtUtc = scheduledForUtc;
                        template.NextRunAtUtc = _scheduleCalculator.GetNextOccurrenceUtc(
                            template.RecurrenceType,
                            template.Interval,
                            template.DayOfMonth,
                            scheduledForUtc);
                        processedOccurrenceCount++;
                    }

                    await _context.SaveChangesAsync(token);
                    return generatedCount;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await OccurrenceWasGeneratedAsync(
                    templateId,
                    attemptedScheduledForUtc,
                    cancellationToken))
            {
                return 0;
            }

            throw;
        }
        catch (DbUpdateException)
        {
            if (await OccurrenceWasGeneratedAsync(
                    templateId,
                    attemptedScheduledForUtc,
                    cancellationToken))
            {
                return 0;
            }

            throw;
        }
    }

    private async Task AddGeneratedTaskAsync(
        RecurringTaskTemplate template,
        TaskAssignmentPlan assignmentPlan,
        DateTime scheduledForUtc,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = template.Title,
            Description = template.Description,
            CreatedBy = template.CreatedByUserId,
            CreatedAt = generatedAtUtc,
            StartDate = scheduledForUtc,
            Status = TaskStatusEnum.NotStarted,
            Priority = template.Priority,
            RequiresReview = template.RequiresReview,
            PlannedEffortHours = template.PlannedEffortHours,
            UnitId = template.UnitId,
            ProjectId = template.ProjectId
        };

        _context.Tasks.Add(task);
        _context.TaskHistories.Add(new TaskHistory
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            ChangedBy = template.CreatedByUserId,
            FieldName = "RecurringTaskGenerated",
            NewValue = $"{template.Id}|{scheduledForUtc:O}",
            ChangedAt = generatedAtUtc
        });

        foreach (var assigneeId in assignmentPlan.UserIds)
        {
            _context.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = assigneeId
            });
            await _notificationService.AddNotification(
                assigneeId,
                $"Bạn vừa được giao công việc định kỳ mới: {task.Title}",
                cancellationToken);
        }

        _context.GeneratedTaskOccurrences.Add(new GeneratedTaskOccurrence
        {
            TemplateId = template.Id,
            ScheduledForUtc = scheduledForUtc,
            TaskId = task.Id,
            GeneratedAtUtc = generatedAtUtc
        });
        await _auditService.RecordAsync(
            AuditEntityTypes.Task,
            task.Id,
            AuditActions.Generated,
            template.CreatedByUserId,
            new
            {
                RecurringTaskTemplateId = template.Id,
                ScheduledForUtc = scheduledForUtc,
                Automated = true
            },
            cancellationToken);
    }

    private async Task EnsureProjectIsAvailableAsync(
        RecurringTaskTemplate template,
        CancellationToken cancellationToken)
    {
        if (!template.ProjectId.HasValue)
            return;

        var project = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == template.ProjectId.Value, cancellationToken)
            ?? throw new BusinessException("Dự án của lịch công việc định kỳ không còn tồn tại.");

        if (project.IsArchived || project.UnitId != template.UnitId)
            throw new BusinessException("Dự án của lịch công việc định kỳ không còn hợp lệ.");
    }

    private async Task<bool> OccurrenceWasGeneratedAsync(
        Guid templateId,
        DateTime? scheduledForUtc,
        CancellationToken cancellationToken)
    {
        return scheduledForUtc.HasValue &&
               await _context.GeneratedTaskOccurrences
                   .AsNoTracking()
                   .AnyAsync(
                       occurrence => occurrence.TemplateId == templateId &&
                                     occurrence.ScheduledForUtc == scheduledForUtc.Value,
                       cancellationToken);
    }
}
