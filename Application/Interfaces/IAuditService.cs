using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IAuditService
    {
        Task RecordAsync(
            string entityType,
            Guid entityId,
            string action,
            Guid? actorUserId,
            object? details = null,
            CancellationToken cancellationToken = default);

        Task<AuditLogPageDto> GetAsync(
            string? entityType,
            Guid? entityId,
            string? action,
            Guid? actorUserId,
            DateTime? from,
            DateTime? to,
            int page,
            int size,
            CancellationToken cancellationToken = default);
    }
}
