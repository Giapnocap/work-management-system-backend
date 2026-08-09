using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IProgressService
    {
        Task<ProgressDto> Update(CreateProgressDto dto, Guid reporterId, CancellationToken cancellationToken = default);
    }
}
