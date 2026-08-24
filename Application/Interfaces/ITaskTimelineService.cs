using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface ITaskTimelineService
{
    Task<TimelinePageDto> GetTaskTimelineAsync(
        Guid taskId,
        Guid requesterId,
        TaskTimelineQueryDto query,
        CancellationToken cancellationToken = default);
}
