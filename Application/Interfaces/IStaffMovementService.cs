using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IStaffMovementService
    {
        Task ValidateChangeAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            Guid? managerBeingReplacedId = null,
            CancellationToken cancellationToken = default);

        Task ApplyChangeAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            Guid? changedBy,
            string reason,
            DateTime changedAt,
            Guid? managerBeingReplacedId = null,
            CancellationToken cancellationToken = default);

        Task DeactivateAsync(
            User user,
            Guid? changedBy,
            DateTime changedAt,
            CancellationToken cancellationToken = default);
    }
}
