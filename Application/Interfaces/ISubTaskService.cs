using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ISubTaskService
    {
        Task<SubTaskDto> AddSubTask(CreateSubTaskDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task ToggleSubTask(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task Delete(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task<List<SubTaskDto>> GetSubTasks(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
    }
}
