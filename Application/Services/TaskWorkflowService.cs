using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Workflows;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class TaskWorkflowService : ITaskWorkflowService
    {
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IAppDbContext _context;
        private readonly TaskWorkflowPolicy _policy;
        private readonly TimeProvider _timeProvider;

        public TaskWorkflowService(
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<Progress> progressRepo,
            IAppDbContext context,
            TaskWorkflowPolicy policy,
            TimeProvider timeProvider)
        {
            _assigneeRepo = assigneeRepo;
            _userRepo = userRepo;
            _progressRepo = progressRepo;
            _context = context;
            _policy = policy;
            _timeProvider = timeProvider;
        }

        public async Task<List<Guid>> ResolveTaskRecipients(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            var directUserIds = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.TaskId == taskId && a.UserId.HasValue)
                .Select(a => a.UserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (directUserIds.Any())
                return directUserIds;

            var unitIds = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.TaskId == taskId && a.UnitId.HasValue)
                .Select(a => a.UnitId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!unitIds.Any())
                return new List<Guid>();

            return await _userRepo.QueryReadOnly()
                .Where(u => u.UnitId.HasValue &&
                            unitIds.Contains(u.UnitId.Value) &&
                            u.Role == SystemRoles.User &&
                            u.IsApproved &&
                            !u.IsDeleted)
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public Task<List<Guid>> GetExpectedUserIds(
            Guid taskId,
            CancellationToken cancellationToken = default)
            => ResolveTaskRecipients(taskId, cancellationToken);

        public ProgressStatusEnum ResolveNewProgressStatus(int percent, bool requiresReview)
            => _policy.ResolveNewProgressStatus(percent, requiresReview);

        public async Task EnsureDependenciesCompletedAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            var blockingTaskTitles = await (
                    from dependency in _context.TaskDependencies.AsNoTracking()
                    join predecessor in _context.Tasks.AsNoTracking()
                        on dependency.DependsOnTaskId equals predecessor.Id
                    where dependency.TaskId == taskId &&
                          predecessor.Status != TaskStatusEnum.Approved
                    orderby predecessor.Title
                    select predecessor.Title)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (blockingTaskTitles.Count > 0)
            {
                throw new BusinessException(
                    $"Cong viec dang bi chan boi: {string.Join(", ", blockingTaskTitles)}.");
            }
        }

        public async Task ApplyProgressReportAsync(
            TaskItem task,
            Progress progress,
            Guid reporterId,
            bool hasPendingSubmittedProgress,
            CancellationToken cancellationToken = default)
        {
            var reason = progress.Status switch
            {
                ProgressStatusEnum.InProgress => "Partial progress reported.",
                ProgressStatusEnum.Submitted => "Completion report submitted for review.",
                ProgressStatusEnum.Approved => "Completion report approved automatically because review is not required.",
                _ => throw new BusinessException("Trang thai bao cao moi khong hop le.")
            };

            switch (progress.Status)
            {
                case ProgressStatusEnum.InProgress:
                    {
                        var targetStatus = hasPendingSubmittedProgress
                            ? TaskStatusEnum.Submitted
                            : TaskStatusEnum.InProgress;
                        var cause = hasPendingSubmittedProgress
                            ? TaskTransitionCause.ProgressSubmitted
                            : TaskTransitionCause.ProgressReported;
                        await TransitionTaskAsync(
                            task,
                            targetStatus,
                            reporterId,
                            reporterId,
                            WorkflowActorType.Assignee,
                            cause,
                            dependenciesCompleted: true,
                            completionRequirementsSatisfied: false,
                            reason,
                            progress.Id,
                            cancellationToken);
                        break;
                    }
                case ProgressStatusEnum.Submitted:
                    await TransitionTaskAsync(
                        task,
                        TaskStatusEnum.Submitted,
                        reporterId,
                        reporterId,
                        WorkflowActorType.Assignee,
                        TaskTransitionCause.ProgressSubmitted,
                        dependenciesCompleted: true,
                        completionRequirementsSatisfied: false,
                        reason,
                        progress.Id,
                        cancellationToken);
                    break;
                case ProgressStatusEnum.Approved:
                    {
                        task.ActualHours += Math.Max(0, progress.HoursSpent);
                        var completionSatisfied = await AreCompletionRequirementsSatisfiedAsync(
                            task.Id,
                            progress.UserId,
                            cancellationToken);
                        var targetStatus = completionSatisfied
                            ? TaskStatusEnum.Approved
                            : TaskStatusEnum.InProgress;
                        await TransitionTaskAsync(
                            task,
                            targetStatus,
                            reporterId,
                            progress.UserId,
                            WorkflowActorType.System,
                            completionSatisfied
                                ? TaskTransitionCause.CompletionRequirementsMet
                                : TaskTransitionCause.CompletionRequirementsPending,
                            dependenciesCompleted: true,
                            completionSatisfied,
                            reason,
                            progress.Id,
                            cancellationToken);
                        break;
                    }
            }

            await RecordProgressTransitionAsync(
                task.Id,
                progress.Id,
                reporterId,
                null,
                progress.Status,
                reason,
                cancellationToken);
        }

        public async Task ApplyReviewDecisionAsync(
            TaskItem task,
            Progress progress,
            bool approve,
            Guid reviewerId,
            bool hasOtherSubmittedProgress,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            var targetProgressStatus = approve
                ? ProgressStatusEnum.Approved
                : ProgressStatusEnum.Rejected;
            var progressContext = new ProgressTransitionContext(
                HasTaskAccess: true,
                HasDecisionReason: approve || normalizedReason != null);

            if (!_policy.CanTransition(
                    progress.Status,
                    targetProgressStatus,
                    WorkflowActorType.Manager,
                    progressContext))
            {
                if (!approve && normalizedReason == null)
                    throw new BusinessException("Tu choi bao cao phai co ly do.");

                throw new BusinessException(
                    $"Khong the chuyen bao cao tu {progress.Status} sang {targetProgressStatus}.");
            }

            if (approve)
                await EnsureDependenciesCompletedAsync(task.Id, cancellationToken);

            var previousProgressStatus = progress.Status;
            if (approve)
                progress.Approve();
            else
                progress.Reject();

            await RecordProgressTransitionAsync(
                task.Id,
                progress.Id,
                reviewerId,
                previousProgressStatus,
                targetProgressStatus,
                normalizedReason ?? "Approved by manager.",
                cancellationToken);

            if (approve)
            {
                task.ActualHours += Math.Max(0, progress.HoursSpent);
                var completionSatisfied = await AreCompletionRequirementsSatisfiedAsync(
                    task.Id,
                    progress.UserId,
                    cancellationToken);
                var targetTaskStatus = completionSatisfied
                    ? TaskStatusEnum.Approved
                    : hasOtherSubmittedProgress
                        ? TaskStatusEnum.Submitted
                        : TaskStatusEnum.InProgress;
                await TransitionTaskAsync(
                    task,
                    targetTaskStatus,
                    reviewerId,
                    progress.UserId,
                    WorkflowActorType.Manager,
                    TaskTransitionCause.ReviewApproved,
                    dependenciesCompleted: true,
                    completionSatisfied,
                    normalizedReason ?? "Progress approved by manager.",
                    progress.Id,
                    cancellationToken);
            }
            else
            {
                var targetTaskStatus = hasOtherSubmittedProgress
                    ? TaskStatusEnum.Submitted
                    : TaskStatusEnum.InProgress;
                await TransitionTaskAsync(
                    task,
                    targetTaskStatus,
                    reviewerId,
                    progress.UserId,
                    WorkflowActorType.Manager,
                    TaskTransitionCause.ReviewRejected,
                    dependenciesCompleted: true,
                    completionRequirementsSatisfied: false,
                    normalizedReason!,
                    progress.Id,
                    cancellationToken);
            }
        }

        public async Task ApplyCompletionStateAsync(
            TaskItem task,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            await EnsureDependenciesCompletedAsync(task.Id, cancellationToken);
            var completionSatisfied = await AreCompletionRequirementsSatisfiedAsync(
                task.Id,
                currentUserId,
                cancellationToken);
            var targetStatus = completionSatisfied
                ? TaskStatusEnum.Approved
                : TaskStatusEnum.InProgress;

            await TransitionTaskAsync(
                task,
                targetStatus,
                currentUserId,
                currentUserId,
                WorkflowActorType.System,
                completionSatisfied
                    ? TaskTransitionCause.CompletionRequirementsMet
                    : TaskTransitionCause.CompletionRequirementsPending,
                dependenciesCompleted: true,
                completionSatisfied,
                completionSatisfied
                    ? "Completion requirements satisfied."
                    : "Waiting for remaining assignees to complete.",
                null,
                cancellationToken);
        }

        private async Task<bool> AreCompletionRequirementsSatisfiedAsync(
            Guid taskId,
            Guid currentUserId,
            CancellationToken cancellationToken)
        {
            var expectedUserIds = await GetExpectedUserIds(taskId, cancellationToken);
            if (!expectedUserIds.Any())
                expectedUserIds.Add(currentUserId);

            var approvedUserIds = await _progressRepo.QueryReadOnly()
                .Where(progress =>
                    progress.TaskId == taskId &&
                    progress.Status == ProgressStatusEnum.Approved &&
                    progress.Percent >= 100)
                .Select(progress => progress.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            approvedUserIds.Add(currentUserId);

            return expectedUserIds.All(approvedUserIds.Contains);
        }

        private async Task TransitionTaskAsync(
            TaskItem task,
            TaskStatusEnum targetStatus,
            Guid changedBy,
            Guid completionOwnerId,
            WorkflowActorType actor,
            TaskTransitionCause cause,
            bool dependenciesCompleted,
            bool completionRequirementsSatisfied,
            string reason,
            Guid? relatedEntityId,
            CancellationToken cancellationToken)
        {
            if (task.Status == targetStatus)
                return;

            var previousStatus = task.Status;
            var transitionContext = new TaskTransitionContext(
                HasTaskAccess: true,
                DependenciesCompleted: dependenciesCompleted,
                CompletionRequirementsSatisfied: completionRequirementsSatisfied);
            if (!_policy.CanTransition(previousStatus, targetStatus, actor, cause, transitionContext))
            {
                throw new BusinessException(
                    $"Khong the chuyen cong viec tu {previousStatus} sang {targetStatus} trong luong hien tai.");
            }

            var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
            switch (targetStatus)
            {
                case TaskStatusEnum.InProgress:
                    task.MarkInProgress();
                    break;
                case TaskStatusEnum.Submitted:
                    task.SubmitForReview();
                    break;
                case TaskStatusEnum.Approved:
                    task.Complete(completionOwnerId, changedAt);
                    break;
                default:
                    throw new BusinessException("Trang thai dich cua cong viec khong hop le.");
            }

            await _context.TaskHistories.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = changedBy,
                FieldName = "Status",
                OldValue = previousStatus.ToString(),
                NewValue = targetStatus.ToString(),
                RelatedEntityId = relatedEntityId,
                Reason = reason,
                ChangedAt = changedAt
            }, cancellationToken);

            if (targetStatus == TaskStatusEnum.Approved)
            {
                await RecordNewlyUnblockedDependentsAsync(
                    task.Id,
                    changedBy,
                    cancellationToken);
            }
        }

        private async Task RecordProgressTransitionAsync(
            Guid taskId,
            Guid progressId,
            Guid changedBy,
            ProgressStatusEnum? previousStatus,
            ProgressStatusEnum targetStatus,
            string reason,
            CancellationToken cancellationToken)
        {
            await _context.TaskHistories.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                ChangedBy = changedBy,
                FieldName = "ProgressStatus",
                OldValue = previousStatus?.ToString(),
                NewValue = targetStatus.ToString(),
                RelatedEntityId = progressId,
                Reason = reason,
                ChangedAt = _timeProvider.GetUtcNow().UtcDateTime
            }, cancellationToken);
        }

        private async Task RecordNewlyUnblockedDependentsAsync(
            Guid completedTaskId,
            Guid changedBy,
            CancellationToken cancellationToken)
        {
            var dependentTaskIds = _context.TaskDependencies
                .AsNoTracking()
                .Where(dependency => dependency.DependsOnTaskId == completedTaskId)
                .Select(dependency => dependency.TaskId);

            var newlyUnblockedTaskIds = await _context.Tasks
                .AsNoTracking()
                .Where(task => dependentTaskIds.Contains(task.Id) &&
                               task.Status != TaskStatusEnum.Approved)
                .Where(task => !_context.TaskDependencies.Any(other =>
                    other.TaskId == task.Id &&
                    other.DependsOnTaskId != completedTaskId &&
                    _context.Tasks.Any(predecessor =>
                        predecessor.Id == other.DependsOnTaskId &&
                        predecessor.Status != TaskStatusEnum.Approved)))
                .Select(task => task.Id)
                .ToListAsync(cancellationToken);

            var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var dependentTaskId in newlyUnblockedTaskIds)
            {
                await _context.TaskHistories.AddAsync(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = dependentTaskId,
                    ChangedBy = changedBy,
                    FieldName = "DependencyUnblocked",
                    OldValue = bool.TrueString,
                    NewValue = bool.FalseString,
                    RelatedEntityId = completedTaskId,
                    Reason = "Blocking predecessor completed.",
                    ChangedAt = changedAt
                }, cancellationToken);
            }
        }
    }
}
