using WorkManagementSystem.Domain.Workflows;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public sealed class TaskWorkflowPolicyTests
{
    private readonly TaskWorkflowPolicy _policy = new();

    [Theory]
    [InlineData(0, false, ProgressStatusEnum.InProgress)]
    [InlineData(99, true, ProgressStatusEnum.InProgress)]
    [InlineData(100, false, ProgressStatusEnum.Approved)]
    [InlineData(100, true, ProgressStatusEnum.Submitted)]
    public void ResolveNewProgressStatus_ReturnsExpectedStatus(
        int percent,
        bool requiresReview,
        ProgressStatusEnum expected)
    {
        Assert.Equal(expected, _policy.ResolveNewProgressStatus(percent, requiresReview));
    }

    [Theory]
    [InlineData(TaskStatusEnum.NotStarted, TaskStatusEnum.InProgress, WorkflowActorType.Assignee, TaskTransitionCause.ProgressReported, false)]
    [InlineData(TaskStatusEnum.NotStarted, TaskStatusEnum.Submitted, WorkflowActorType.Assignee, TaskTransitionCause.ProgressSubmitted, false)]
    [InlineData(TaskStatusEnum.InProgress, TaskStatusEnum.Submitted, WorkflowActorType.Assignee, TaskTransitionCause.ProgressSubmitted, false)]
    [InlineData(TaskStatusEnum.NotStarted, TaskStatusEnum.Approved, WorkflowActorType.System, TaskTransitionCause.CompletionRequirementsMet, true)]
    [InlineData(TaskStatusEnum.InProgress, TaskStatusEnum.Approved, WorkflowActorType.System, TaskTransitionCause.CompletionRequirementsMet, true)]
    [InlineData(TaskStatusEnum.Submitted, TaskStatusEnum.Approved, WorkflowActorType.Manager, TaskTransitionCause.ReviewApproved, true)]
    [InlineData(TaskStatusEnum.Submitted, TaskStatusEnum.InProgress, WorkflowActorType.Manager, TaskTransitionCause.ReviewApproved, false)]
    [InlineData(TaskStatusEnum.Submitted, TaskStatusEnum.InProgress, WorkflowActorType.Manager, TaskTransitionCause.ReviewRejected, false)]
    public void CanTransition_ValidTaskMatrix_ReturnsTrue(
        TaskStatusEnum from,
        TaskStatusEnum to,
        WorkflowActorType actor,
        TaskTransitionCause cause,
        bool completionSatisfied)
    {
        var context = new TaskTransitionContext(
            HasTaskAccess: true,
            DependenciesCompleted: true,
            CompletionRequirementsSatisfied: completionSatisfied);

        Assert.True(_policy.CanTransition(from, to, actor, cause, context));
    }

    [Theory]
    [InlineData(TaskStatusEnum.Approved, TaskStatusEnum.InProgress, WorkflowActorType.Manager, TaskTransitionCause.ReviewRejected)]
    [InlineData(TaskStatusEnum.NotStarted, TaskStatusEnum.Approved, WorkflowActorType.Manager, TaskTransitionCause.ReviewApproved)]
    [InlineData(TaskStatusEnum.Submitted, TaskStatusEnum.Approved, WorkflowActorType.Assignee, TaskTransitionCause.ProgressSubmitted)]
    [InlineData(TaskStatusEnum.InProgress, TaskStatusEnum.Submitted, WorkflowActorType.Manager, TaskTransitionCause.ReviewApproved)]
    [InlineData(TaskStatusEnum.InProgress, TaskStatusEnum.InProgress, WorkflowActorType.Assignee, TaskTransitionCause.ProgressReported)]
    public void CanTransition_InvalidTaskMatrix_ReturnsFalse(
        TaskStatusEnum from,
        TaskStatusEnum to,
        WorkflowActorType actor,
        TaskTransitionCause cause)
    {
        var context = new TaskTransitionContext(
            HasTaskAccess: true,
            DependenciesCompleted: true,
            CompletionRequirementsSatisfied: true);

        Assert.False(_policy.CanTransition(from, to, actor, cause, context));
    }

    [Fact]
    public void CanTransition_TaskWithoutResourceAccess_ReturnsFalse()
    {
        var context = new TaskTransitionContext(
            HasTaskAccess: false,
            DependenciesCompleted: true,
            CompletionRequirementsSatisfied: false);

        Assert.False(_policy.CanTransition(
            TaskStatusEnum.NotStarted,
            TaskStatusEnum.InProgress,
            WorkflowActorType.Assignee,
            TaskTransitionCause.ProgressReported,
            context));
    }

    [Fact]
    public void CanTransition_CompletionWithBlockedDependency_ReturnsFalse()
    {
        var context = new TaskTransitionContext(
            HasTaskAccess: true,
            DependenciesCompleted: false,
            CompletionRequirementsSatisfied: true);

        Assert.False(_policy.CanTransition(
            TaskStatusEnum.Submitted,
            TaskStatusEnum.Approved,
            WorkflowActorType.Manager,
            TaskTransitionCause.ReviewApproved,
            context));
    }

    [Theory]
    [InlineData(ProgressStatusEnum.Approved, true)]
    [InlineData(ProgressStatusEnum.Rejected, true)]
    public void CanTransition_ValidProgressReview_ReturnsTrue(
        ProgressStatusEnum target,
        bool hasReason)
    {
        var context = new ProgressTransitionContext(
            HasTaskAccess: true,
            HasDecisionReason: hasReason);

        Assert.True(_policy.CanTransition(
            ProgressStatusEnum.Submitted,
            target,
            WorkflowActorType.Manager,
            context));
    }

    [Theory]
    [InlineData(ProgressStatusEnum.Submitted, ProgressStatusEnum.Rejected, WorkflowActorType.Manager, true, false)]
    [InlineData(ProgressStatusEnum.InProgress, ProgressStatusEnum.Approved, WorkflowActorType.Manager, true, true)]
    [InlineData(ProgressStatusEnum.Submitted, ProgressStatusEnum.Approved, WorkflowActorType.Assignee, true, true)]
    [InlineData(ProgressStatusEnum.Submitted, ProgressStatusEnum.Approved, WorkflowActorType.Manager, false, true)]
    public void CanTransition_InvalidProgressReview_ReturnsFalse(
        ProgressStatusEnum from,
        ProgressStatusEnum to,
        WorkflowActorType actor,
        bool hasAccess,
        bool hasReason)
    {
        var context = new ProgressTransitionContext(hasAccess, hasReason);

        Assert.False(_policy.CanTransition(from, to, actor, context));
    }
}
