using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IKpiPeriodResolver
    {
        Task<KpiPeriod> ResolveAsync(
            Guid? periodId = null,
            CancellationToken cancellationToken = default);
    }
}
