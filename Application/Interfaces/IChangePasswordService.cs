using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IChangePasswordService
    {
        Task ChangePassword(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
    }
}
