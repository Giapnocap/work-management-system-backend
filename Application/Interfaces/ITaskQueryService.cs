using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.Interfaces;

public interface ITaskQueryService
{
    Task<PagedResult<TaskDto>> Get(
        string keyword,
        int page,
        int size,
        string? status,
        Guid requesterId,
        bool myTasks = false,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);

    Task<TaskDto> GetByIdAsync(
        Guid id,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    Task<List<TaskHistoryDto>> GetHistoryAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default);
}
