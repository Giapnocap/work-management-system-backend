using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.Interfaces;

public interface IProgressQueryService
{
    Task<PagedResult<ProgressDto>> GetAll(
        int page,
        int size,
        Guid requesterId,
        bool myProgress = false,
        CancellationToken cancellationToken = default);

    Task<List<ProgressDto>> GetByTaskAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ProgressDto>> GetMyHistory(
        Guid requesterId,
        int page,
        int size,
        CancellationToken cancellationToken = default);
}
