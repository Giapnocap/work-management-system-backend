using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskService
    {
        Task<TaskDto> Create(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<TaskDto> Update(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken = default);
        Task Delete(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task RemindTask(Guid taskId, Guid reminderId, CancellationToken cancellationToken = default);
    }
}
