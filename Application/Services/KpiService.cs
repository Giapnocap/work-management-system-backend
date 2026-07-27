using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class KpiService : IKpiService
    {
        private readonly AppDbContext _context;
        private readonly IUserPerformanceService _performanceService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditService _auditService;
        private readonly IKpiPeriodResolver _periodResolver;

        public KpiService(
            AppDbContext context,
            IUserPerformanceService performanceService,
            ITransactionManager transactionManager,
            IAuditService auditService,
            IKpiPeriodResolver periodResolver)
        {
            _context = context;
            _performanceService = performanceService;
            _transactionManager = transactionManager;
            _auditService = auditService;
            _periodResolver = periodResolver;
        }

        public async Task<List<KpiPeriodDto>> GetPeriods(CancellationToken cancellationToken = default)
        {
            return await _context.KpiPeriods
                .AsNoTracking()
                .OrderByDescending(p => p.StartDate)
                .Select(p => MapPeriod(p))
                .ToListAsync(cancellationToken);
        }

        public async Task<KpiPeriodDto> GetCurrentPeriod(CancellationToken cancellationToken = default)
        {
            var current = await _periodResolver.ResolveAsync(null, cancellationToken);
            return MapPeriod(current);
        }

        public Task<KpiPeriodDto> CreatePeriod(
            CreateKpiPeriodDto dto,
            Guid createdBy,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => CreatePeriodCore(dto, createdBy, token),
                cancellationToken);

        private async Task<KpiPeriodDto> CreatePeriodCore(
            CreateKpiPeriodDto dto,
            Guid createdBy,
            CancellationToken cancellationToken)
        {
            var startDate = NormalizeStartOfDay(dto.StartDate);
            var endDate = NormalizeEndOfDay(dto.EndDate);

            if (endDate <= startDate)
                throw new BusinessException("Ngay ket thuc phai lon hon ngay bat dau.");

            var overlaps = await _context.KpiPeriods.AnyAsync(
                p => p.StartDate <= endDate && p.EndDate >= startDate,
                cancellationToken);
            if (overlaps)
                throw new BusinessException("Ky KPI bi trung khoang thoi gian voi ky da ton tai.");

            var period = new KpiPeriod
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(dto.Name) ? $"KPI {startDate:MM/yyyy}" : dto.Name.Trim(),
                Type = string.IsNullOrWhiteSpace(dto.Type) ? "Monthly" : dto.Type.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                Status = "Open"
            };

            _context.KpiPeriods.Add(period);
            await _auditService.RecordAsync(
                AuditEntityTypes.KpiPeriod,
                period.Id,
                AuditActions.Created,
                createdBy,
                new { period.Name, period.Type, period.StartDate, period.EndDate },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return MapPeriod(period);
        }

        public Task<List<PerformanceDto>> LockPeriod(
            Guid periodId,
            Guid lockedBy,
            CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => LockPeriodCore(periodId, lockedBy, token),
                cancellationToken);

        private async Task<List<PerformanceDto>> LockPeriodCore(
            Guid periodId,
            Guid lockedBy,
            CancellationToken cancellationToken)
        {
            var period = await _context.KpiPeriods.FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
                ?? throw new NotFoundException("KPI period not found");

            if (period.Status == "Locked")
            {
                return await _performanceService.GetUnitPerformanceAsync(lockedBy, periodId, cancellationToken);
            }

            var activeUserIds = _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user =>
                    user.Role != "Admin" &&
                    user.IsApproved &&
                    !user.IsDeleted &&
                    user.JoinedUnitAt <= period.EndDate)
                .Select(user => user.Id);

            var historicalUserIds = _context.UserWorkHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(history =>
                    history.Role != "Admin" &&
                    history.EffectiveFrom <= period.EndDate &&
                    (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= period.StartDate))
                .Select(history => history.UserId);

            var candidateUserIds = await activeUserIds
                .Union(historicalUserIds)
                .ToListAsync(cancellationToken);

            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => candidateUserIds.Contains(user.Id) && user.IsApproved)
                .ToListAsync(cancellationToken);

            var userIds = users.Select(u => u.Id).ToList();
            var existingResults = await _context.KpiResults
                .Where(r => r.PeriodId == period.Id && userIds.Contains(r.UserId))
                .ToDictionaryAsync(r => r.UserId, cancellationToken);
            var unitNames = await _context.Units
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);

            var results = new List<PerformanceDto>();
            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dto = await _performanceService.GetPerformanceAsync(user.Id, period.Id, cancellationToken);
                results.Add(dto);

                if (!existingResults.TryGetValue(user.Id, out var existing))
                {
                    existing = new KpiResult
                    {
                        Id = Guid.NewGuid(),
                        PeriodId = period.Id,
                        UserId = user.Id
                    };
                    _context.KpiResults.Add(existing);
                }

                existing.UnitId = dto.UnitId;
                existing.Role = string.IsNullOrWhiteSpace(dto.Role) ? user.Role : dto.Role;
                existing.FullNameSnapshot = string.IsNullOrWhiteSpace(dto.FullName)
                    ? user.FullName
                    : dto.FullName;
                existing.EmployeeCodeSnapshot = string.IsNullOrWhiteSpace(dto.EmployeeCode)
                    ? user.EmployeeCode
                    : dto.EmployeeCode;
                existing.UnitNameSnapshot = ResolveUnitName(dto, unitNames);
                existing.EffectiveFrom = dto.EffectiveFrom ?? period.StartDate;
                existing.EffectiveTo = dto.EffectiveTo ?? period.EndDate;
                existing.Score = dto.Score;
                existing.Level = dto.Level;
                existing.TotalTasks = dto.TotalTasks;
                existing.CompletedOnTime = dto.CompletedOnTime;
                existing.CompletedLate = dto.CompletedLate;
                existing.OverdueTasks = dto.OverdueTasks;
                existing.RejectedReports = dto.RejectedReports;
                existing.BonusPoints = dto.BonusPoints;
                existing.PenaltyPoints = dto.PenaltyPoints;
                existing.ReviewPenaltyPoints = dto.ReviewPenaltyPoints;
                existing.UnitAverageScore = dto.UnitAverageScore;
                existing.PersonalScore = dto.PersonalScore;
                existing.IsManagerKpi = dto.IsManagerKpi;
                existing.IsAtRisk = dto.IsAtRisk;
                existing.WarningMessage = dto.WarningMessage;
                existing.CalculatedAt = DateTime.UtcNow;
                existing.LockedAt = DateTime.UtcNow;
            }

            period.Status = "Locked";
            period.LockedAt = DateTime.UtcNow;
            period.LockedBy = lockedBy;
            await _auditService.RecordAsync(
                AuditEntityTypes.KpiPeriod,
                period.Id,
                AuditActions.Locked,
                lockedBy,
                new { period.Name, ResultCount = results.Count },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private static string ResolveUnitName(
            PerformanceDto performance,
            IReadOnlyDictionary<Guid, string> unitNames)
        {
            if (!string.IsNullOrWhiteSpace(performance.UnitName))
                return performance.UnitName;

            return performance.UnitId.HasValue &&
                   unitNames.TryGetValue(performance.UnitId.Value, out var unitName)
                ? unitName
                : string.Empty;
        }

        private static DateTime NormalizeStartOfDay(DateTime value)
            => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

        private static DateTime NormalizeEndOfDay(DateTime value)
            => DateTime.SpecifyKind(value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        private static KpiPeriodDto MapPeriod(KpiPeriod period)
        {
            return new KpiPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                Type = period.Type,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                Status = period.Status,
                CreatedAt = period.CreatedAt,
                LockedAt = period.LockedAt
            };
        }
    }
}
