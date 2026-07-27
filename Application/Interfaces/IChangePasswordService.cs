using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IChangePasswordService
    {
        Task<string> ChangePassword(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
    }
}
