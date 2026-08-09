namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskAccessService
    {
        Task<bool> CanAccessTask(Guid taskId, Guid userId, bool managementOnly = false, CancellationToken cancellationToken = default);
        Task<bool> CanManageUnit(Guid unitId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CanAccessUpload(Guid uploadId, Guid userId, CancellationToken cancellationToken = default);
        Task<string?> GetUserRole(Guid userId, CancellationToken cancellationToken = default);
    }
}
