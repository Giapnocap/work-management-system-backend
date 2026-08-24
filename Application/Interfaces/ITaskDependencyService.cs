using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface ITaskDependencyService
{
    Task<TaskDependencyDto> AddAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<List<TaskDependencyDto>> GetAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    Task<TaskDependencyGraphDto> GetGraphAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default);
}
