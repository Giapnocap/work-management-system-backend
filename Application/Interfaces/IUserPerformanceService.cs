using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUserPerformanceService
    {
        Task<bool> CanViewPerformanceAsync(
            Guid requesterId,
            Guid targetUserId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default);

        Task<PerformanceDto> GetPerformanceAsync(Guid userId, Guid? periodId = null, CancellationToken cancellationToken = default);
        Task<List<PerformanceDto>> GetUnitPerformanceAsync(Guid requesterId, Guid? periodId = null, CancellationToken cancellationToken = default);
    }
}
