using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IKpiService
    {
        Task<List<KpiPeriodDto>> GetPeriods(CancellationToken cancellationToken = default);
        Task<KpiPeriodDto> GetCurrentPeriod(CancellationToken cancellationToken = default);
        Task<KpiPeriodDto> CreatePeriod(CreateKpiPeriodDto dto, Guid createdBy, CancellationToken cancellationToken = default);
        Task<List<PerformanceDto>> LockPeriod(Guid periodId, Guid lockedBy, CancellationToken cancellationToken = default);
    }
}
