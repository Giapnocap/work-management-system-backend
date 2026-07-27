using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IProgressService
    {
        Task<ProgressDto> Update(CreateProgressDto dto, Guid reporterId, CancellationToken cancellationToken = default);
        Task<object> GetAll(int page, int size, Guid? userId = null, Guid? unitId = null, CancellationToken cancellationToken = default);
        Task<List<ProgressDto>> GetByTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
        Task<object> GetMyHistory(Guid userId, int page, int size, CancellationToken cancellationToken = default);
    }
}
