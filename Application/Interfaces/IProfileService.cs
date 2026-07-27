using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileDto?> GetProfile(Guid userId, CancellationToken cancellationToken = default);
        Task<string> UpdateProfile(Guid userId, ProfileDto dto, CancellationToken cancellationToken = default);
    }
}
