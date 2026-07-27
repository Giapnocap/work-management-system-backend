using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUserTaskAssignmentService
    {
        Task EnsureCanChangeAssignmentAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            CancellationToken cancellationToken = default);

        Task EnsureCanDeleteAsync(User user, CancellationToken cancellationToken = default);
    }
}
