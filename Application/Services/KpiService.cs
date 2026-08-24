using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class KpiService : IKpiService
    {
        private readonly IAppDbContext _context;
        private readonly IUserPerformanceService _performanceService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditService _auditService;
        private readonly IKpiPeriodResolver _periodResolver;

        public KpiService(
            IAppDbContext context,
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

        public async Task<KpiDashboardDto> GetDashboard(
            Guid periodId,
            Guid requesterId,
            CancellationToken cancellationToken = default)
        {
            var requester = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == requesterId)
                .Select(user => new { user.Role, user.UnitId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ForbiddenException("Bạn không có quyền xem dashboard KPI.");

            if (requester.Role != SystemRoles.Admin && requester.Role != SystemRoles.Manager)
                throw new ForbiddenException("Bạn không có quyền xem dashboard KPI.");

            if (requester.Role == SystemRoles.Manager && !requester.UnitId.HasValue)
                throw new ForbiddenException("Quản lý chưa được gắn phòng ban.");

            var period = await _periodResolver.ResolveAsync(periodId, cancellationToken);
            var performances = await _performanceService.GetUnitPerformanceAsync(
                requesterId,
                period.Id,
                cancellationToken);

            var unitIds = performances
                .Where(performance => performance.UnitId.HasValue)
                .Select(performance => performance.UnitId!.Value)
                .Distinct()
                .ToList();

            if (requester.UnitId.HasValue && !unitIds.Contains(requester.UnitId.Value))
                unitIds.Add(requester.UnitId.Value);

            var unitNames = unitIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Units
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(unit => unitIds.Contains(unit.Id))
                    .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);

            foreach (var performance in performances)
            {
                if (string.IsNullOrWhiteSpace(performance.UnitName) &&
                    performance.UnitId.HasValue &&
                    unitNames.TryGetValue(performance.UnitId.Value, out var unitName))
                {
                    performance.UnitName = unitName;
                }
            }

            var orderedPerformances = performances
                .OrderByDescending(performance => performance.Score)
                .ThenBy(performance => performance.FullName)
                .ToList();
            var units = orderedPerformances
                .GroupBy(performance => new { performance.UnitId, performance.UnitName })
                .Select(group => new KpiUnitInsightDto
                {
                    UnitId = group.Key.UnitId,
                    UnitName = group.Key.UnitName,
                    Summary = BuildSummary(group)
                })
                .OrderBy(unit => unit.UnitName)
                .ToList();
            var scopeUnitName = requester.UnitId.HasValue &&
                                unitNames.TryGetValue(requester.UnitId.Value, out var requesterUnitName)
                ? requesterUnitName
                : string.Empty;

            return new KpiDashboardDto
            {
                Period = MapPeriod(period),
                Scope = requester.Role == SystemRoles.Admin ? "Organization" : "Unit",
                ScopeUnitId = requester.Role == SystemRoles.Manager ? requester.UnitId : null,
                ScopeUnitName = requester.Role == SystemRoles.Manager ? scopeUnitName : string.Empty,
                Summary = BuildSummary(orderedPerformances),
                Units = units,
                Users = orderedPerformances,
                Formula = BuildFormula(ResolveFormulaVersion(orderedPerformances)),
                UsageNotice = "KPI chỉ là dữ liệu tham khảo quản trị, không được dùng để tự động ra quyết định nhân sự."
            };
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
                throw new BusinessException("Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            var overlaps = await _context.KpiPeriods.AnyAsync(
                p => p.StartDate <= endDate && p.EndDate >= startDate,
                cancellationToken);
            if (overlaps)
                throw new BusinessException("Kỳ KPI bị trùng khoảng thời gian với kỳ đã tồn tại.");

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
                ?? throw new NotFoundException("Không tìm thấy kỳ KPI.");

            if (period.Status == "Locked")
            {
                return await _performanceService.GetUnitPerformanceAsync(lockedBy, periodId, cancellationToken);
            }

            var activeUserIds = _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user =>
                    user.Role != SystemRoles.Admin &&
                    user.IsApproved &&
                    !user.IsDeleted &&
                    user.JoinedUnitAt <= period.EndDate)
                .Select(user => user.Id);

            var historicalUserIds = _context.UserWorkHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(history =>
                    history.Role != SystemRoles.Admin &&
                    history.EffectiveFrom <= period.EndDate &&
                    (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= period.StartDate))
                .Select(history => history.UserId);

            var candidateUserIds = activeUserIds.Union(historicalUserIds);

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

            var results = (await _performanceService.GetPerformancesAsync(
                userIds,
                period.Id,
                cancellationToken)).ToList();
            var resultsByUser = results.ToDictionary(result => result.UserId);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!resultsByUser.TryGetValue(user.Id, out var dto))
                    continue;

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
                existing.CompletedTasks = dto.CompletedTasks;
                existing.CompletedOnTime = dto.CompletedOnTime;
                existing.CompletedLate = dto.CompletedLate;
                existing.OverdueTasks = dto.OverdueTasks;
                existing.RejectedReports = dto.RejectedReports;
                existing.ProgressReportCount = dto.ProgressReportCount;
                existing.PlannedEffortHours = dto.PlannedEffortHours;
                existing.ActualHours = dto.ActualHours;
                existing.BonusPoints = dto.BonusPoints;
                existing.PenaltyPoints = dto.PenaltyPoints;
                existing.ReviewPenaltyPoints = dto.ReviewPenaltyPoints;
                existing.UnitAverageScore = dto.UnitAverageScore;
                existing.PersonalScore = dto.PersonalScore;
                existing.IsManagerKpi = dto.IsManagerKpi;
                existing.IsAtRisk = dto.IsAtRisk;
                existing.WarningMessage = dto.WarningMessage;
                existing.FormulaVersion = dto.FormulaVersion;
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

        private static KpiInsightSummaryDto BuildSummary(IEnumerable<PerformanceDto> performances)
        {
            var items = performances.ToList();
            var totalTasks = items.Sum(item => item.TotalTasks);
            var completedTasks = items.Sum(item => item.CompletedTasks);
            var overdueTasks = items.Sum(item => item.OverdueTasks);
            var rejectedReports = items.Sum(item => item.RejectedReports);
            var progressReportCount = items.Sum(item => item.ProgressReportCount);
            var plannedEffortHours = items.Sum(item => item.PlannedEffortHours);
            var actualHours = items.Sum(item => item.ActualHours);

            return new KpiInsightSummaryDto
            {
                UserCount = items.Count,
                AtRiskUserCount = items.Count(item => item.IsAtRisk),
                AverageScore = items.Count == 0
                    ? 0m
                    : Math.Round(items.Average(item => (decimal)item.Score), 2, MidpointRounding.AwayFromZero),
                TotalTasks = totalTasks,
                Throughput = completedTasks,
                OverdueTasks = overdueTasks,
                RejectedReports = rejectedReports,
                ProgressReportCount = progressReportCount,
                PlannedEffortHours = plannedEffortHours,
                ActualHours = actualHours,
                CompletionRate = KpiFormula.CalculateRate(completedTasks, totalTasks),
                OverdueRate = KpiFormula.CalculateRate(overdueTasks, totalTasks),
                ReviewRejectionRate = KpiFormula.CalculateRate(rejectedReports, progressReportCount),
                EstimationAccuracy = KpiFormula.CalculateEstimationAccuracy(plannedEffortHours, actualHours)
            };
        }

        private static string ResolveFormulaVersion(IReadOnlyCollection<PerformanceDto> performances)
        {
            var versions = performances
                .Select(performance => performance.FormulaVersion)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return versions.Count switch
            {
                0 => KpiFormula.Version,
                1 => versions[0],
                _ => "Mixed"
            };
        }

        private static KpiFormulaDto BuildFormula(string version)
            => new()
            {
                Version = version,
                PersonalBaseScore = KpiFormula.BaseScore,
                PersonalMaximumScore = KpiFormula.MaximumPersonalScore,
                OnTimeCompletionBonus = KpiFormula.OnTimeCompletionBonus,
                NoDeadlineCompletionBonus = KpiFormula.NoDeadlineCompletionBonus,
                RejectedReportPenalty = KpiFormula.RejectedReportPenalty,
                FirstOverduePenalty = KpiFormula.FirstOverduePenalty,
                SecondOverduePenalty = KpiFormula.SecondOverduePenalty,
                LaterOverduePenalty = KpiFormula.LaterOverduePenalty,
                ManagerUnitWeightPercent = (int)Math.Round(KpiFormula.ManagerUnitWeight * 100),
                ManagerPersonalWeightPercent = (int)Math.Round(KpiFormula.ManagerPersonalWeight * 100),
                UsedForAutomaticHrDecisions = false
            };

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
                LockedAt = period.LockedAt,
                RowVersion = period.RowVersion
            };
        }
    }
}
