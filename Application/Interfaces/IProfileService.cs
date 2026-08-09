using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileDto> GetProfile(Guid userId, CancellationToken cancellationToken = default);
        Task<ProfileDto> UpdateProfile(Guid userId, ProfileDto dto, CancellationToken cancellationToken = default);
    }
}
