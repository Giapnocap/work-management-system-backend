using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public sealed class KpiPeriodResolver : IKpiPeriodResolver
    {
        private readonly IAppDbContext _context;

        public KpiPeriodResolver(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<KpiPeriod> ResolveAsync(
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            var periods = _context.KpiPeriods.AsNoTracking();

            if (periodId.HasValue)
            {
                return await periods.FirstOrDefaultAsync(
                    period => period.Id == periodId.Value,
                    cancellationToken)
                    ?? throw new NotFoundException("KPI period not found");
            }

            var now = DateTime.UtcNow;
            return await periods
                .OrderByDescending(period => period.StartDate)
                .FirstOrDefaultAsync(
                    period => period.StartDate <= now && period.EndDate >= now,
                    cancellationToken)
                ?? throw new NotFoundException(
                    "Khong co ky KPI cho thoi gian hien tai. Admin can tao ky KPI truoc khi xem hieu suat.");
        }
    }
}
