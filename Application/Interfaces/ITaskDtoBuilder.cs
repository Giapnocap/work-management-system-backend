using WorkManagementSystem.Application.DTOs;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskDtoBuilder
    {
        Task<TaskDto> BuildTaskDto(
            TaskItem task,
            CancellationToken cancellationToken = default);
        Task<List<TaskDto>> BuildTaskDtos(
            IReadOnlyCollection<TaskItem> tasks,
            CancellationToken cancellationToken = default);
    }
}
