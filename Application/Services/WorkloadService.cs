using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services;

public sealed class WorkloadService : IWorkloadService
{
    private const string NormalLevel = "Normal";
    private const string BusyLevel = "Busy";
    private const string OverloadedLevel = "Overloaded";

    private readonly IAppDbContext _context;
    private readonly ITaskBusinessRuleService _taskRules;
    private readonly ITransactionManager _transactionManager;
    private readonly IAuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly WorkloadOptions _options;

    public WorkloadService(
        IAppDbContext context,
        ITaskBusinessRuleService taskRules,
        ITransactionManager transactionManager,
        IAuditService auditService,
        TimeProvider timeProvider,
        IOptions<WorkloadOptions> options)
    {
        _context = context;
        _taskRules = taskRules;
        _transactionManager = transactionManager;
        _auditService = auditService;
        _timeProvider = timeProvider;
        _options = options.Value;

        if (!_options.IsValid())
            throw new InvalidOperationException("Workload configuration is invalid.");
    }

    public async Task<WorkloadSummaryDto> GetWorkloadAsync(
        Guid requesterId,
        DateTime? from,
        DateTime? to,
        Guid? unitId,
        CancellationToken cancellationToken = default)
    {
        var range = NormalizeQueryRange(from, to);
        var scope = await ResolveScopeAsync(requesterId, unitId, cancellationToken);
        var users = await LoadScopedUsersAsync(scope, cancellationToken);
        var workloads = await BuildWorkloadsAsync(users, range, cancellationToken);
        var ordered = workloads
            .OrderByDescending(workload => workload.WorkloadPercent)
            .ThenByDescending(workload => workload.OverdueTaskCount)
            .ThenBy(workload => workload.FullName)
            .ToList();

        return new WorkloadSummaryDto
        {
            From = range.From,
            To = range.ToInclusive,
            UnitId = scope.UnitId,
            BusyThresholdPercent = _options.BusyThresholdPercent,
            OverloadedThresholdPercent = _options.OverloadedThresholdPercent,
            UserCount = ordered.Count,
            BusyUserCount = ordered.Count(item => item.Level == BusyLevel),
            OverloadedUserCount = ordered.Count(item => item.Level == OverloadedLevel),
            Users = ordered
        };
    }

    public async Task<UserWorkloadDto> GetUserWorkloadAsync(
        Guid requesterId,
        Guid userId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var range = NormalizeQueryRange(from, to);
        var scope = await ResolveScopeAsync(requesterId, null, cancellationToken);
        var user = await LoadTargetEmployeeAsync(userId, scope, cancellationToken);
        var workloads = await BuildWorkloadsAsync(new[] { user }, range, cancellationToken);
        return workloads.Single();
    }

    public Task<UserCapacityDto> UpdateCapacityAsync(
        Guid requesterId,
        Guid userId,
        UpdateUserCapacityDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.WeeklyCapacityHours is <= 0m or > 168m)
            throw new BusinessException("Sức chứa tuần phải lớn hơn 0 và không vượt quá 168 giờ.");

        return _transactionManager.ExecuteSerializableAsync(
            token => UpdateCapacityCoreAsync(requesterId, userId, dto, token),
            cancellationToken);
    }

    public async Task<AssignmentPreviewDto> PreviewAssignmentAsync(
        Guid managerId,
        AssignmentPreviewRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        ValidatePlannedEffort(dto.PlannedEffortHours);

        var manager = await LoadManagerAsync(managerId, cancellationToken);
        var assignmentPlan = await _taskRules.ResolveAssignmentPlan(
            dto.UserIds,
            dto.UnitIds,
            manager.UnitId!.Value,
            cancellationToken);

        return await BuildAssignmentPreviewAsync(
            manager,
            assignmentPlan.UserIds,
            dto.PlannedEffortHours,
            dto.StartDate,
            dto.DueDate,
            cancellationToken);
    }

    public async Task<AssignmentPreviewDto> PreviewResolvedAssignmentAsync(
        Guid managerId,
        IReadOnlyCollection<Guid> userIds,
        decimal plannedEffortHours,
        DateTime? startDate,
        DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        ValidatePlannedEffort(plannedEffortHours);
        if (userIds.Count == 0)
            throw new BusinessException("Không có nhân viên để dự báo khối lượng.");

        var manager = await LoadManagerAsync(managerId, cancellationToken);
        return await BuildAssignmentPreviewAsync(
            manager,
            userIds,
            plannedEffortHours,
            startDate,
            dueDate,
            cancellationToken);
    }

    private async Task<UserCapacityDto> UpdateCapacityCoreAsync(
        Guid requesterId,
        Guid userId,
        UpdateUserCapacityDto dto,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(requesterId, null, cancellationToken);
        await LoadTargetEmployeeAsync(userId, scope, cancellationToken);

        var effectiveFrom = AsUtcDate(dto.EffectiveFrom ?? _timeProvider.GetUtcNow().UtcDateTime);
        var histories = await _context.UserCapacities
            .Where(capacity => capacity.UserId == userId)
            .OrderByDescending(capacity => capacity.EffectiveFrom)
            .Take(2)
            .ToListAsync(cancellationToken);

        var openHistories = histories.Where(capacity => !capacity.EffectiveTo.HasValue).ToList();
        if (openHistories.Count > 1)
            throw new BusinessException("Dữ liệu sức chứa không hợp lệ: có nhiều giai đoạn đang mở.");

        var current = openHistories.SingleOrDefault();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        decimal? oldWeeklyHours = null;
        UserCapacity result;

        if (current != null)
        {
            if (effectiveFrom < current.EffectiveFrom)
                throw new BusinessException("Ngày áp dụng mới không được sớm hơn sức chứa hiện tại.");

            oldWeeklyHours = current.WeeklyCapacityHours;
            if (effectiveFrom == current.EffectiveFrom)
            {
                current.WeeklyCapacityHours = dto.WeeklyCapacityHours;
                current.ChangedByUserId = requesterId;
                current.CreatedAt = now;
                result = current;
            }
            else
            {
                current.EffectiveTo = effectiveFrom;
                result = CreateCapacity(userId, requesterId, dto.WeeklyCapacityHours, effectiveFrom, now);
                await _context.UserCapacities.AddAsync(result, cancellationToken);
            }
        }
        else
        {
            var latest = histories.FirstOrDefault();
            if (latest?.EffectiveTo is DateTime latestEnd && effectiveFrom < latestEnd)
                throw new BusinessException("Ngày áp dụng sức chứa bị chồng lặp với lịch sử hiện có.");

            result = CreateCapacity(userId, requesterId, dto.WeeklyCapacityHours, effectiveFrom, now);
            await _context.UserCapacities.AddAsync(result, cancellationToken);
        }

        await _auditService.RecordAsync(
            "UserCapacity",
            result.Id,
            oldWeeklyHours.HasValue ? "Updated" : "Created",
            requesterId,
            new
            {
                userId,
                oldWeeklyCapacityHours = oldWeeklyHours,
                newWeeklyCapacityHours = dto.WeeklyCapacityHours,
                effectiveFrom
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapCapacity(result);
    }

    private async Task<AssignmentPreviewDto> BuildAssignmentPreviewAsync(
        RequesterScope manager,
        IReadOnlyCollection<Guid> userIds,
        decimal plannedEffortHours,
        DateTime? startDate,
        DateTime? dueDate,
        CancellationToken cancellationToken)
    {
        var range = NormalizePreviewRange(startDate, dueDate);
        var distinctUserIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctUserIds.Count == 0)
            throw new BusinessException("Không có nhân viên để dự báo khối lượng.");

        var users = await LoadScopedUsersAsync(
            manager,
            cancellationToken,
            distinctUserIds);

        if (users.Count != distinctUserIds.Count)
            throw new ForbiddenException("Chỉ được dự báo giao việc cho nhân viên trong phòng ban của bạn.");

        var workloads = await BuildWorkloadsAsync(users, range, cancellationToken);
        var workloadMap = workloads.ToDictionary(item => item.UserId);
        var effortPerAssignee = plannedEffortHours / distinctUserIds.Count;
        var projections = distinctUserIds.Select(userId =>
        {
            var current = workloadMap[userId];
            var projectedHours = current.RemainingWorkHours + effortPerAssignee;
            var projectedPercent = CalculatePercent(projectedHours, current.CapacityHours);
            var projectedLevel = ResolveLevel(projectedPercent);

            return new AssignmentWorkloadDto
            {
                UserId = current.UserId,
                FullName = current.FullName,
                EmployeeCode = current.EmployeeCode,
                CurrentWorkloadHours = current.RemainingWorkHours,
                ProjectedWorkloadHours = Round(projectedHours),
                CapacityHours = current.CapacityHours,
                CurrentWorkloadPercent = current.WorkloadPercent,
                ProjectedWorkloadPercent = Round(projectedPercent),
                ProjectedLevel = projectedLevel,
                HasWarning = projectedPercent >= _options.BusyThresholdPercent
            };
        }).ToList();

        return new AssignmentPreviewDto
        {
            From = range.From,
            To = range.ToInclusive,
            PlannedEffortHours = plannedEffortHours,
            EffortPerAssigneeHours = Round(effortPerAssignee),
            HasWarning = projections.Any(item => item.HasWarning),
            Assignees = projections
        };
    }

    private async Task<List<UserWorkloadDto>> BuildWorkloadsAsync(
        IReadOnlyCollection<WorkloadUserRow> users,
        DateRange range,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
            return new List<UserWorkloadDto>();

        var userIds = users.Select(user => user.Id).ToList();
        var nullableUserIds = userIds.Select(userId => (Guid?)userId).ToList();
        var capacityRows = await _context.UserCapacities
            .AsNoTracking()
            .Where(capacity => userIds.Contains(capacity.UserId) &&
                               capacity.EffectiveFrom < range.ToExclusive &&
                               (!capacity.EffectiveTo.HasValue || capacity.EffectiveTo > range.From))
            .OrderBy(capacity => capacity.UserId)
            .ThenBy(capacity => capacity.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var assignmentCounts = _context.TaskAssignees
            .AsNoTracking()
            .Where(assignment => assignment.UserId.HasValue)
            .GroupBy(assignment => assignment.TaskId)
            .Select(group => new
            {
                TaskId = group.Key,
                AssigneeCount = group.Count()
            });

        var today = AsUtcDate(_timeProvider.GetUtcNow().UtcDateTime);
        var aggregates = await (
                from assignment in _context.TaskAssignees.AsNoTracking()
                join task in _context.Tasks.AsNoTracking()
                    on assignment.TaskId equals task.Id
                join assignmentCount in assignmentCounts
                    on task.Id equals assignmentCount.TaskId
                where assignment.UserId.HasValue &&
                      nullableUserIds.Contains(assignment.UserId) &&
                      task.Status != TaskStatusEnum.Approved &&
                      (task.StartDate ?? task.CreatedAt) < range.ToExclusive &&
                      (!task.DueDate.HasValue || task.DueDate.Value >= range.From)
                group new { task, assignmentCount } by assignment.UserId
                into grouped
                select new WorkloadAggregateRow
                {
                    UserId = grouped.Key ?? Guid.Empty,
                    RemainingWorkHours = grouped.Sum(item =>
                        item.task.PlannedEffortHours.HasValue &&
                        item.task.PlannedEffortHours.Value > item.task.ActualHours
                            ? (item.task.PlannedEffortHours.Value - item.task.ActualHours) /
                              item.assignmentCount.AssigneeCount
                            : 0m),
                    ActiveTaskCount = grouped.Count(),
                    OverdueTaskCount = grouped.Sum(item =>
                        item.task.DueDate.HasValue && item.task.DueDate.Value < today ? 1 : 0)
                })
            .ToListAsync(cancellationToken);

        var capacityLookup = capacityRows.ToLookup(capacity => capacity.UserId);
        var aggregateMap = aggregates.ToDictionary(row => row.UserId);

        return users.Select(user =>
        {
            var capacityHours = CalculateCapacityHours(capacityLookup[user.Id], range);
            aggregateMap.TryGetValue(user.Id, out var aggregate);
            var remainingHours = aggregate?.RemainingWorkHours ?? 0m;
            var workloadPercent = CalculatePercent(remainingHours, capacityHours);

            return new UserWorkloadDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                EmployeeCode = user.EmployeeCode,
                UnitId = user.UnitId,
                UnitName = user.UnitName,
                CapacityHours = Round(capacityHours),
                RemainingWorkHours = Round(remainingHours),
                WorkloadPercent = Round(workloadPercent),
                Level = ResolveLevel(workloadPercent),
                ActiveTaskCount = aggregate?.ActiveTaskCount ?? 0,
                OverdueTaskCount = aggregate?.OverdueTaskCount ?? 0
            };
        }).ToList();
    }

    private async Task<RequesterScope> ResolveScopeAsync(
        Guid requesterId,
        Guid? requestedUnitId,
        CancellationToken cancellationToken)
    {
        var requester = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == requesterId)
            .Select(user => new RequesterScope(user.Role, user.UnitId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (requester.Role == SystemRoles.Admin)
            return requester with { UnitId = requestedUnitId };

        if (requester.Role != SystemRoles.Manager)
            throw new ForbiddenException("Chỉ Admin hoặc Trưởng phòng mới được xem khối lượng công việc.");
        if (!requester.UnitId.HasValue)
            throw new BusinessException("Trưởng phòng chưa thuộc phòng ban nào.");
        if (requestedUnitId.HasValue && requestedUnitId != requester.UnitId)
            throw new ForbiddenException("Trưởng phòng chỉ được xem khối lượng công việc trong phòng ban của mình.");

        return requester;
    }

    private async Task<RequesterScope> LoadManagerAsync(
        Guid managerId,
        CancellationToken cancellationToken)
    {
        var manager = await ResolveScopeAsync(managerId, null, cancellationToken);
        if (manager.Role != SystemRoles.Manager)
            throw new ForbiddenException("Chỉ Trưởng phòng mới được dự báo giao việc.");
        return manager;
    }

    private async Task<List<WorkloadUserRow>> LoadScopedUsersAsync(
        RequesterScope scope,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? selectedUserIds = null)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(user => user.Role == SystemRoles.User &&
                           user.IsApproved &&
                           user.UnitId.HasValue);

        if (scope.UnitId.HasValue)
            query = query.Where(user => user.UnitId == scope.UnitId.Value);
        if (selectedUserIds != null)
            query = query.Where(user => selectedUserIds.Contains(user.Id));

        return await query
            .OrderBy(user => user.FullName)
            .Select(user => new WorkloadUserRow
            {
                Id = user.Id,
                FullName = user.FullName,
                EmployeeCode = user.EmployeeCode,
                UnitId = user.UnitId!.Value,
                UnitName = user.Unit!.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<WorkloadUserRow> LoadTargetEmployeeAsync(
        Guid userId,
        RequesterScope scope,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new WorkloadUserRow
            {
                Id = candidate.Id,
                FullName = candidate.FullName,
                EmployeeCode = candidate.EmployeeCode,
                Role = candidate.Role,
                IsApproved = candidate.IsApproved,
                UnitId = candidate.UnitId ?? Guid.Empty,
                UnitName = candidate.Unit != null ? candidate.Unit.Name : string.Empty
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (scope.UnitId.HasValue && user.UnitId != scope.UnitId.Value)
            throw new ForbiddenException("Trưởng phòng chỉ được quản lý sức chứa trong phòng ban của mình.");
        if (user.Role != SystemRoles.User || !user.IsApproved || user.UnitId == Guid.Empty)
            throw new BusinessException("Sức chứa chỉ áp dụng cho nhân viên đang hoạt động trong phòng ban.");

        return user;
    }

    private decimal CalculateCapacityHours(
        IEnumerable<UserCapacity> capacityRows,
        DateRange range)
    {
        var cursor = range.From;
        var total = 0m;

        foreach (var capacity in capacityRows.OrderBy(item => item.EffectiveFrom))
        {
            var segmentStart = capacity.EffectiveFrom > range.From
                ? capacity.EffectiveFrom
                : range.From;
            var segmentEnd = !capacity.EffectiveTo.HasValue || capacity.EffectiveTo > range.ToExclusive
                ? range.ToExclusive
                : capacity.EffectiveTo.Value;

            if (segmentEnd <= cursor)
                continue;
            if (segmentStart > cursor)
                total += CapacityForDuration(_options.DefaultWeeklyCapacityHours, segmentStart - cursor);

            var effectiveStart = segmentStart > cursor ? segmentStart : cursor;
            total += CapacityForDuration(capacity.WeeklyCapacityHours, segmentEnd - effectiveStart);
            cursor = segmentEnd;
        }

        if (cursor < range.ToExclusive)
            total += CapacityForDuration(_options.DefaultWeeklyCapacityHours, range.ToExclusive - cursor);

        return total;
    }

    private static decimal CapacityForDuration(decimal weeklyHours, TimeSpan duration)
        => weeklyHours * (decimal)duration.TotalDays / 7m;

    private decimal CalculatePercent(decimal hours, decimal capacityHours)
        => capacityHours <= 0m ? 0m : hours * 100m / capacityHours;

    private string ResolveLevel(decimal workloadPercent)
    {
        if (workloadPercent >= _options.OverloadedThresholdPercent)
            return OverloadedLevel;
        if (workloadPercent >= _options.BusyThresholdPercent)
            return BusyLevel;
        return NormalLevel;
    }

    private DateRange NormalizeQueryRange(DateTime? from, DateTime? to)
    {
        var range = NormalizeRange(from, to);
        var days = (range.ToInclusive - range.From).Days + 1;
        if (days > _options.MaxRangeDays)
            throw new BusinessException($"Khoảng xem khối lượng công việc không được vượt quá {_options.MaxRangeDays} ngày.");
        return range;
    }

    private DateRange NormalizePreviewRange(DateTime? startDate, DateTime? dueDate)
        => NormalizeRange(startDate, dueDate);

    private DateRange NormalizeRange(DateTime? from, DateTime? to)
    {
        DateTime fromDate;
        DateTime toDate;

        if (!from.HasValue && !to.HasValue)
        {
            var today = AsUtcDate(_timeProvider.GetUtcNow().UtcDateTime);
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            fromDate = today.AddDays(-daysSinceMonday);
            toDate = fromDate.AddDays(6);
        }
        else if (from.HasValue && !to.HasValue)
        {
            fromDate = AsUtcDate(from.Value);
            if (fromDate > DateTime.MaxValue.Date.AddDays(-6))
                throw new BusinessException("Ngày bắt đầu xem khối lượng công việc không hợp lệ.");
            toDate = fromDate.AddDays(6);
        }
        else if (!from.HasValue)
        {
            toDate = AsUtcDate(to!.Value);
            if (toDate < DateTime.MinValue.Date.AddDays(6))
                throw new BusinessException("Ngày kết thúc xem khối lượng công việc không hợp lệ.");
            fromDate = toDate.AddDays(-6);
        }
        else
        {
            fromDate = AsUtcDate(from.Value);
            toDate = AsUtcDate(to!.Value);
        }

        if (toDate < fromDate)
            throw new BusinessException("Ngày kết thúc xem khối lượng công việc không được sớm hơn ngày bắt đầu.");
        if (toDate == DateTime.MaxValue.Date)
            throw new BusinessException("Ngày kết thúc xem khối lượng công việc không hợp lệ.");

        return new DateRange(fromDate, toDate, toDate.AddDays(1));
    }

    private static UserCapacity CreateCapacity(
        Guid userId,
        Guid requesterId,
        decimal weeklyHours,
        DateTime effectiveFrom,
        DateTime createdAt)
    {
        return new UserCapacity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeeklyCapacityHours = weeklyHours,
            EffectiveFrom = effectiveFrom,
            CreatedAt = createdAt,
            ChangedByUserId = requesterId
        };
    }

    private static UserCapacityDto MapCapacity(UserCapacity capacity)
    {
        return new UserCapacityDto
        {
            Id = capacity.Id,
            UserId = capacity.UserId,
            WeeklyCapacityHours = capacity.WeeklyCapacityHours,
            EffectiveFrom = capacity.EffectiveFrom,
            EffectiveTo = capacity.EffectiveTo
        };
    }

    private static DateTime AsUtcDate(DateTime value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static void ValidatePlannedEffort(decimal plannedEffortHours)
    {
        if (plannedEffortHours is <= 0m or > 100000m)
        {
            throw new BusinessException(
                "Khối lượng kế hoạch phải lớn hơn 0 và không vượt quá 100000 giờ.");
        }
    }

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record RequesterScope(string Role, Guid? UnitId);
    private sealed record DateRange(DateTime From, DateTime ToInclusive, DateTime ToExclusive);

    private sealed class WorkloadUserRow
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public Guid UnitId { get; set; }
        public string UnitName { get; set; } = string.Empty;
    }

    private sealed class WorkloadAggregateRow
    {
        public Guid UserId { get; set; }
        public decimal RemainingWorkHours { get; set; }
        public int ActiveTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
    }
}
