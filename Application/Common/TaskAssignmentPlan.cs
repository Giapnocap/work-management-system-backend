namespace WorkManagementSystem.Application.Common
{
    public sealed record TaskAssignmentPlan(IReadOnlyList<Guid> UserIds, bool IsDepartmentAssignment);
}
