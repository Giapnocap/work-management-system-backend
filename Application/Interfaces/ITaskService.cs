using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskService
    {
        Task<TaskDto> Create(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<object> Get(string keyword, int page, int size, string? status, Guid currentUserId, Guid? userId = null, Guid? unitId = null, Guid? projectId = null, CancellationToken cancellationToken = default);
        Task<TaskDto> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task<List<TaskHistory>> GetHistoryAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
        Task<TaskDto> Update(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken = default);
        Task Delete(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task<Guid?> GetManagerUnitId(Guid managerId, CancellationToken cancellationToken = default);
        Task RemindTask(Guid taskId, Guid reminderId, CancellationToken cancellationToken = default);
    }
}
