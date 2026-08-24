using WorkManagementSystem.Domain.Enums;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Domain.Workflows;

public enum WorkflowActorType
{
    Assignee = 0,
    Manager = 1,
    System = 2
}

public enum TaskTransitionCause
{
    ProgressReported = 0,
    ProgressSubmitted = 1,
    CompletionRequirementsMet = 2,
    CompletionRequirementsPending = 3,
    ReviewApproved = 4,
    ReviewRejected = 5
}

public readonly record struct TaskTransitionContext(
    bool HasTaskAccess,
    bool DependenciesCompleted,
    bool CompletionRequirementsSatisfied);

public readonly record struct ProgressTransitionContext(
    bool HasTaskAccess,
    bool HasDecisionReason);

public sealed class TaskWorkflowPolicy
{
    public ProgressStatus ResolveNewProgressStatus(int percent, bool requiresReview)
    {
        if (percent < 100)
            return ProgressStatus.InProgress;

        return requiresReview
            ? ProgressStatus.Submitted
            : ProgressStatus.Approved;
    }

    public bool CanTransition(
        TaskStatusEnum from,
        TaskStatusEnum to,
        WorkflowActorType actor,
        TaskTransitionCause cause,
        TaskTransitionContext context)
    {
        if (from == to || !context.HasTaskAccess || from == TaskStatusEnum.Approved)
            return false;

        return (from, to, actor, cause) switch
        {
            (TaskStatusEnum.NotStarted or TaskStatusEnum.InProgress,
                TaskStatusEnum.InProgress,
                WorkflowActorType.Assignee,
                TaskTransitionCause.ProgressReported)
                => context.DependenciesCompleted,

            (TaskStatusEnum.NotStarted or TaskStatusEnum.InProgress,
                TaskStatusEnum.Submitted,
                WorkflowActorType.Assignee,
                TaskTransitionCause.ProgressSubmitted)
                => context.DependenciesCompleted,

            (TaskStatusEnum.NotStarted or TaskStatusEnum.InProgress or TaskStatusEnum.Submitted,
                TaskStatusEnum.Approved,
                WorkflowActorType.System,
                TaskTransitionCause.CompletionRequirementsMet)
                => context.DependenciesCompleted && context.CompletionRequirementsSatisfied,

            (TaskStatusEnum.NotStarted,
                TaskStatusEnum.InProgress,
                WorkflowActorType.System,
                TaskTransitionCause.CompletionRequirementsPending)
                => context.DependenciesCompleted && !context.CompletionRequirementsSatisfied,

            (TaskStatusEnum.Submitted,
                TaskStatusEnum.Approved,
                WorkflowActorType.Manager,
                TaskTransitionCause.ReviewApproved)
                => context.DependenciesCompleted && context.CompletionRequirementsSatisfied,

            (TaskStatusEnum.Submitted,
                TaskStatusEnum.InProgress,
                WorkflowActorType.Manager,
                TaskTransitionCause.ReviewApproved or TaskTransitionCause.ReviewRejected)
                => cause == TaskTransitionCause.ReviewRejected ||
                   (context.DependenciesCompleted && !context.CompletionRequirementsSatisfied),

            _ => false
        };
    }

    public bool CanTransition(
        ProgressStatus from,
        ProgressStatus to,
        WorkflowActorType actor,
        ProgressTransitionContext context)
    {
        if (!context.HasTaskAccess || actor != WorkflowActorType.Manager || from != ProgressStatus.Submitted)
            return false;

        return to switch
        {
            ProgressStatus.Approved => true,
            ProgressStatus.Rejected => context.HasDecisionReason,
            _ => false
        };
    }
}
