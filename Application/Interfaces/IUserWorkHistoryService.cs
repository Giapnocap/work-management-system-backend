using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUserWorkHistoryService
    {
        Task RecordChangeAsync(
            User user,
            Guid? newUnitId,
            string newRole,
            Guid? changedBy,
            string reason,
            DateTime changedAt,
            CancellationToken cancellationToken = default);

        Task CloseCurrentAsync(
            User user,
            Guid? changedBy,
            DateTime changedAt,
            CancellationToken cancellationToken = default);
    }
}
