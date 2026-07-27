using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> Register(AuthDto dto, CancellationToken cancellationToken = default);
        Task<string> Login(string username, string password, CancellationToken cancellationToken = default);
        Task<string> ResetPassword(
            ResetPasswordDto dto,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default);
        Task<string> ApproveUser(
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default);
        Task<string> RejectUser(
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default);
        Task<List<UserDto>> GetPendingUsers(CancellationToken cancellationToken = default);
        Task<string> RefreshToken(Guid userId, CancellationToken cancellationToken = default);
    }
}
