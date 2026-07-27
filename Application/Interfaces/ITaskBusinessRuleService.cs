using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskBusinessRuleService
    {
        Task<Project?> ValidateProjectScope(
            Guid? projectId,
            Guid taskUnitId,
            User manager,
            CancellationToken cancellationToken = default);
        Task<TaskAssignmentPlan> ResolveAssignmentPlan(
            IEnumerable<Guid> directUserIds,
            IEnumerable<Guid> unitIds,
            Guid managerUnitId,
            CancellationToken cancellationToken = default);
        void EnsureCanEdit(TaskItem task);
        Task EnsureCanDelete(TaskItem task, CancellationToken cancellationToken = default);
    }
}
