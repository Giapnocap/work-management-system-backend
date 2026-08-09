using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class UserPerformanceService : IUserPerformanceService
    {
        private static readonly HashSet<Guid> EmptyTaskIds = new();

        private readonly IGenericRepository<User> _repo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IAppDbContext _context;
        private readonly IKpiPeriodResolver _periodResolver;

        public UserPerformanceService(
            IGenericRepository<User> repo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<Progress> progressRepo,
            IAppDbContext context,
            IKpiPeriodResolver periodResolver)
        {
            _repo = repo;
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _progressRepo = progressRepo;
            _context = context;
            _periodResolver = periodResolver;
        }

        public async Task<bool> CanViewPerformanceAsync(
            Guid requesterId,
            Guid targetUserId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            var requester = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == requesterId)
                .Select(user => new { user.Id, user.Role, user.UnitId })
                .FirstOrDefaultAsync(cancellationToken);
            if (requester == null)
                return false;

            var target = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.Id == targetUserId)
                .Select(user => new
                {
                    user.Id,
                    user.Role,
                    user.UnitId,
                    user.JoinedUnitAt,
                    user.IsDeleted
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (target == null)
                return false;

            if (requester.Role == SystemRoles.Admin || requester.Id == target.Id)
                return true;

            if (requester.Role != SystemRoles.Manager || !requester.UnitId.HasValue || target.Role == SystemRoles.Admin)
                return false;

            var period = await _periodResolver.ResolveAsync(periodId, cancellationToken);
            var managerUnitId = requester.UnitId.Value;

            if (period.Status == "Locked")
            {
                var snapshotMatchesUnit = await _context.KpiResults
                    .AsNoTracking()
                    .AnyAsync(
                        result =>
                            result.PeriodId == period.Id &&
                            result.UserId == targetUserId &&
                            result.UnitId == managerUnitId,
                        cancellationToken);
                if (snapshotMatchesUnit)
                    return true;
            }

            var overlappingHistories = await _context.UserWorkHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(history =>
                    history.UserId == targetUserId &&
                    history.EffectiveFrom <= period.EndDate &&
                    (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= period.StartDate))
                .Select(history => history.UnitId)
                .ToListAsync(cancellationToken);

            if (overlappingHistories.Count > 0)
                return overlappingHistories.Contains(managerUnitId);

            return !target.IsDeleted &&
                   target.UnitId == managerUnitId &&
                   target.JoinedUnitAt <= period.EndDate;
        }

        public async Task<PerformanceDto> GetPerformanceAsync(
            Guid userId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
                ?? throw new NotFoundException("User not found");

            var period = await _periodResolver.ResolveAsync(periodId, cancellationToken);
            if (period.Status == "Locked")
            {
                var snapshot = await _context.KpiResults.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.PeriodId == period.Id && r.UserId == userId, cancellationToken);
                if (snapshot != null)
                    return MapKpiResult(snapshot, period);
            }

            var histories = await GetOverlappingHistoriesAsync(user, period, cancellationToken);
            return await CalculatePerformanceAsync(
                user,
                period,
                histories,
                DateTime.UtcNow,
                batchData: null,
                cancellationToken);
        }

        public async Task<IReadOnlyList<PerformanceDto>> GetPerformancesAsync(
            IReadOnlyCollection<Guid> userIds,
            Guid periodId,
            CancellationToken cancellationToken = default)
        {
            var distinctUserIds = userIds
                .Where(userId => userId != Guid.Empty)
                .Distinct()
                .ToList();
            if (distinctUserIds.Count == 0)
                return Array.Empty<PerformanceDto>();

            var period = await _periodResolver.ResolveAsync(periodId, cancellationToken);
            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => distinctUserIds.Contains(user.Id))
                .ToListAsync(cancellationToken);

            var snapshots = period.Status == "Locked"
                ? await _context.KpiResults
                    .AsNoTracking()
                    .Where(result => result.PeriodId == period.Id && distinctUserIds.Contains(result.UserId))
                    .ToDictionaryAsync(result => result.UserId, cancellationToken)
                : new Dictionary<Guid, KpiResult>();

            var calculationUsers = users
                .Where(user => !snapshots.ContainsKey(user.Id))
                .ToList();
            var calculationUserIds = calculationUsers.Select(user => user.Id).ToList();

            var histories = calculationUserIds.Count == 0
                ? new List<UserWorkHistory>()
                : await _context.UserWorkHistories
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(history =>
                        calculationUserIds.Contains(history.UserId) &&
                        history.EffectiveFrom <= period.EndDate &&
                        (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= period.StartDate))
                    .OrderBy(history => history.EffectiveFrom)
                    .ToListAsync(cancellationToken);

            var historiesByUser = histories
                .GroupBy(history => history.UserId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var batchData = await LoadBatchPersonalKpiDataAsync(
                calculationUserIds,
                period,
                cancellationToken);
            var now = DateTime.UtcNow;
            var results = new List<PerformanceDto>(users.Count);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshots.TryGetValue(user.Id, out var snapshot))
                {
                    results.Add(MapKpiResult(snapshot, period));
                    continue;
                }

                var userHistories = historiesByUser.TryGetValue(user.Id, out var persistedHistories)
                    ? persistedHistories
                    : CreateFallbackHistories(user, period);
                results.Add(await CalculatePerformanceAsync(
                    user,
                    period,
                    userHistories,
                    now,
                    batchData,
                    cancellationToken));
            }

            return results;
        }

        private async Task<PerformanceDto> CalculatePerformanceAsync(
            User user,
            KpiPeriod period,
            IReadOnlyList<UserWorkHistory> histories,
            DateTime now,
            BatchPersonalKpiData? batchData,
            CancellationToken cancellationToken)
        {
            var segmentDtos = new List<PerformanceDto>();

            foreach (var history in histories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var from = MaxDate(period.StartDate, history.EffectiveFrom);
                var to = MinDate(period.EndDate, history.EffectiveTo ?? period.EndDate);
                if (from > to) continue;

                var personalDto = batchData == null
                    ? await CalculatePersonalPerformanceDtoAsync(
                        user.Id,
                        user,
                        now,
                        history.UnitId,
                        period,
                        from,
                        to,
                        history.Role,
                        cancellationToken)
                    : CalculatePersonalPerformanceDto(
                        user,
                        now,
                        history.UnitId,
                        period,
                        from,
                        to,
                        history.Role,
                        batchData);

                var dto = history.Role == SystemRoles.Manager
                    ? await CalculateManagerPerformanceAsync(
                        user.Id,
                        user,
                        now,
                        period,
                        history,
                        from,
                        to,
                        personalDto,
                        cancellationToken)
                    : personalDto;

                segmentDtos.Add(dto);
            }

            if (segmentDtos.Count == 0)
                return ApplyPeriodMetadata(
                    CreateEmptyPerformance(user.Id, user),
                    period,
                    user.UnitId,
                    user.Role,
                    period.StartDate,
                    period.EndDate,
                    false);

            return MergeSegmentPerformance(user, period, segmentDtos);
        }

        private async Task<PerformanceDto> CalculatePersonalPerformanceDtoAsync(
            Guid userId,
            User user,
            DateTime now,
            Guid? filterUnitId,
            KpiPeriod period,
            DateTime from,
            DateTime to,
            string roleForPeriod,
            CancellationToken cancellationToken)
        {
            var assignedTaskIds = _assigneeRepo.QueryReadOnly()
                .Where(a => a.UserId == userId)
                .Select(a => a.TaskId);

            var taskQuery = _taskRepo.QueryReadOnly()
                .Where(t => assignedTaskIds.Contains(t.Id) && !t.IsDeleted)
                .Where(t => !filterUnitId.HasValue || t.UnitId == filterUnitId.Value)
                .Where(t => t.CreatedAt <= to && (!t.CompletedAt.HasValue || t.CompletedAt.Value >= from));
            var eligibleTaskIds = taskQuery.Select(task => task.Id);
            var tasks = await taskQuery.ToListAsync(cancellationToken);

            var progressList = await _context.Progresses
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.UserId == userId && eligibleTaskIds.Contains(p.TaskId))
                .Where(p => p.UpdatedAt >= from && p.UpdatedAt <= to)
                .ToListAsync(cancellationToken);

            var metrics = CalculatePersonalKpiMetrics(tasks, progressList, now, from, to);
            var dto = BuildPersonalPerformanceDto(userId, user, metrics);

            return ApplyPeriodMetadata(dto, period, filterUnitId, roleForPeriod, from, to, period.Status == "Locked");
        }

        private async Task<BatchPersonalKpiData> LoadBatchPersonalKpiDataAsync(
            IReadOnlyCollection<Guid> userIds,
            KpiPeriod period,
            CancellationToken cancellationToken)
        {
            if (userIds.Count == 0)
                return BatchPersonalKpiData.Empty;

            var assignees = await _assigneeRepo.QueryReadOnly()
                .Where(assignee => assignee.UserId.HasValue && userIds.Contains(assignee.UserId.Value))
                .ToListAsync(cancellationToken);
            var taskIdsByUser = assignees
                .GroupBy(assignee => assignee.UserId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(assignee => assignee.TaskId).ToHashSet());
            var taskIds = assignees
                .Select(assignee => assignee.TaskId)
                .Distinct()
                .ToList();

            var tasks = taskIds.Count == 0
                ? new List<TaskItem>()
                : await _taskRepo.QueryReadOnly()
                    .Where(task => taskIds.Contains(task.Id) && !task.IsDeleted)
                    .Where(task =>
                        task.CreatedAt <= period.EndDate &&
                        (!task.CompletedAt.HasValue || task.CompletedAt.Value >= period.StartDate))
                    .ToListAsync(cancellationToken);
            var relevantTaskIds = tasks.Select(task => task.Id).ToList();

            var progresses = relevantTaskIds.Count == 0
                ? new List<Progress>()
                : await _context.Progresses
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(progress =>
                        userIds.Contains(progress.UserId) &&
                        relevantTaskIds.Contains(progress.TaskId) &&
                        progress.UpdatedAt >= period.StartDate &&
                        progress.UpdatedAt <= period.EndDate)
                    .ToListAsync(cancellationToken);
            var progressesByUser = progresses
                .GroupBy(progress => progress.UserId)
                .ToDictionary(group => group.Key, group => group.ToList());

            return new BatchPersonalKpiData(taskIdsByUser, tasks, progressesByUser);
        }

        private PerformanceDto CalculatePersonalPerformanceDto(
            User user,
            DateTime now,
            Guid? filterUnitId,
            KpiPeriod period,
            DateTime from,
            DateTime to,
            string roleForPeriod,
            BatchPersonalKpiData batchData)
        {
            if (!batchData.TaskIdsByUser.TryGetValue(user.Id, out var assignedTaskIds))
                assignedTaskIds = EmptyTaskIds;

            var tasks = batchData.Tasks
                .Where(task => assignedTaskIds.Contains(task.Id))
                .Where(task => !filterUnitId.HasValue || task.UnitId == filterUnitId.Value)
                .Where(task => task.CreatedAt <= to && (!task.CompletedAt.HasValue || task.CompletedAt.Value >= from))
                .ToList();
            var taskIds = tasks.Select(task => task.Id).ToHashSet();

            var progresses = batchData.ProgressesByUser.TryGetValue(user.Id, out var userProgresses)
                ? userProgresses
                    .Where(progress => taskIds.Contains(progress.TaskId))
                    .Where(progress => progress.UpdatedAt >= from && progress.UpdatedAt <= to)
                    .ToList()
                : new List<Progress>();

            var metrics = CalculatePersonalKpiMetrics(tasks, progresses, now, from, to);
            var dto = BuildPersonalPerformanceDto(user.Id, user, metrics);
            return ApplyPeriodMetadata(dto, period, filterUnitId, roleForPeriod, from, to, period.Status == "Locked");
        }

        private async Task<PerformanceDto> CalculateManagerPerformanceAsync(
            Guid managerId,
            User user,
            DateTime now,
            KpiPeriod period,
            UserWorkHistory history,
            DateTime from,
            DateTime to,
            PerformanceDto personalDto,
            CancellationToken cancellationToken)
        {
            int personalScore = personalDto.TotalTasks == 0 ? 100 : personalDto.Score;

            double unitAvgScore = 100;
            var unitPerformanceList = new List<PerformanceDto>();

            if (history.UnitId.HasValue)
            {
                var unitId = history.UnitId.Value;
                unitPerformanceList = await BatchCalculateUnitKpiAsync(unitId, now, period, from, to, cancellationToken);

                if (unitPerformanceList.Count > 0)
                {
                    var activeMembers = unitPerformanceList.Where(p => p.TotalTasks > 0).ToList();
                    if (activeMembers.Count > 0)
                        unitAvgScore = activeMembers.Average(p => p.Score);
                    else
                        unitAvgScore = 100;
                }
            }

            var memberIdsForReview = unitPerformanceList.Select(p => p.UserId).ToList();
            var pendingProgresses = await _progressRepo.QueryReadOnly()
                .Where(p => memberIdsForReview.Contains(p.UserId) && p.Status == ProgressStatusEnum.Submitted)
                .Where(p => p.UpdatedAt >= from && p.UpdatedAt <= to)
                .ToListAsync(cancellationToken);

            int reviewPenaltyCount = 0;
            foreach (var p in pendingProgresses)
            {
                var hoursSinceSubmitted = (now - p.UpdatedAt).TotalHours;
                var hoursSinceJoined = (now - history.EffectiveFrom).TotalHours;

                if (hoursSinceSubmitted > 48 && hoursSinceJoined > 72)
                {
                    reviewPenaltyCount++;
                }
            }

            int reviewPenaltyPoints = Math.Min(reviewPenaltyCount * 3, 15);

            int finalScore = (int)Math.Round(unitAvgScore * 0.7 + personalScore * 0.3) - reviewPenaltyPoints;
            finalScore = Math.Max(0, finalScore);

            string level, levelColor, levelIcon;
            if (finalScore >= 90) { level = "Xuat sac"; levelColor = "green"; levelIcon = "*"; }
            else if (finalScore >= 75) { level = "Tot"; levelColor = "blue"; levelIcon = "+"; }
            else if (finalScore >= 60) { level = "Trung binh"; levelColor = "yellow"; levelIcon = "!"; }
            else { level = "Yeu"; levelColor = "red"; levelIcon = "!"; }

            bool isAtRisk = finalScore < 60 || reviewPenaltyCount > 0 || personalDto.IsAtRisk;
            List<string> warnings = new List<string>();

            if (reviewPenaltyCount > 0)
                warnings.Add($"Nut that co chai: '{reviewPenaltyCount}' bao cao bi ngam chua duyet qua 48h!");

            if (finalScore < 60)
                warnings.Add("Hieu suat lanh dao phong ban thap, anh huong diem KPI quan ly!");

            if (!string.IsNullOrEmpty(personalDto.WarningMessage))
                warnings.Add(personalDto.WarningMessage);

            string warning = string.Join(" | ", warnings);

            var dto = new PerformanceDto
            {
                UserId = managerId,
                FullName = user.FullName ?? "-",
                EmployeeCode = user.EmployeeCode ?? "-",
                Score = finalScore,
                Level = level,
                LevelColor = levelColor,
                LevelIcon = levelIcon,
                TotalTasks = personalDto.TotalTasks,
                CompletedOnTime = personalDto.CompletedOnTime,
                CompletedLate = personalDto.CompletedLate,
                OverdueTasks = personalDto.OverdueTasks,
                RejectedReports = personalDto.RejectedReports,
                BonusPoints = personalDto.BonusPoints,
                PenaltyPoints = personalDto.PenaltyPoints,
                ReviewPenaltyPoints = reviewPenaltyPoints,
                IsManagerKpi = true,
                UnitAverageScore = unitAvgScore,
                PersonalScore = personalScore,
                IsAtRisk = isAtRisk,
                WarningMessage = warning
            };

            return ApplyPeriodMetadata(dto, period, history.UnitId, history.Role, from, to, period.Status == "Locked");
        }

        public async Task<List<PerformanceDto>> GetUnitPerformanceAsync(
            Guid requesterId,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            var requester = await _repo.GetByIdAsync(requesterId, cancellationToken);
            if (requester == null) return new List<PerformanceDto>();

            var period = await _periodResolver.ResolveAsync(periodId, cancellationToken);
            if (period.Status == "Locked")
            {
                var snapshotQuery = _context.KpiResults.AsNoTracking()
                    .Where(r => r.PeriodId == period.Id);

                if (requester.Role == SystemRoles.Manager)
                {
                    if (!requester.UnitId.HasValue) return new List<PerformanceDto>();
                    snapshotQuery = snapshotQuery.Where(r => r.UnitId == requester.UnitId.Value && r.Role == SystemRoles.User);
                }
                else if (requester.Role != SystemRoles.Admin)
                {
                    return new List<PerformanceDto>();
                }

                var snapshots = await snapshotQuery
                    .OrderByDescending(r => r.Score)
                    .ToListAsync(cancellationToken);
                return snapshots
                    .Select(snapshot => MapKpiResult(snapshot, period))
                    .OrderByDescending(performance => performance.Score)
                    .ToList();
            }

            if (requester.Role == SystemRoles.Manager)
            {
                if (!requester.UnitId.HasValue) return new List<PerformanceDto>();
                var result = await BatchCalculateUnitKpiAsync(
                    requester.UnitId.Value,
                    DateTime.UtcNow,
                    period,
                    period.StartDate,
                    period.EndDate,
                    cancellationToken);
                return result.OrderByDescending(p => p.Score).ToList();
            }

            if (requester.Role == SystemRoles.Admin)
            {
                var userIds = await _repo.QueryReadOnly()
                    .Where(u => u.Role != SystemRoles.Admin && u.IsApproved && !u.IsDeleted)
                    .Select(user => user.Id)
                    .ToListAsync(cancellationToken);

                var result = await GetPerformancesAsync(userIds, period.Id, cancellationToken);

                return result.OrderByDescending(p => p.Score).ToList();
            }

            return new List<PerformanceDto>();
        }

        private async Task<List<PerformanceDto>> BatchCalculateUnitKpiAsync(
            Guid unitId,
            DateTime now,
            KpiPeriod period,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            var histories = await _context.UserWorkHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(h => h.UnitId == unitId
                            && h.Role == SystemRoles.User
                            && h.EffectiveFrom <= to
                            && (!h.EffectiveTo.HasValue || h.EffectiveTo.Value >= from))
                .ToListAsync(cancellationToken);

            var memberIdsFromHistory = histories.Select(h => h.UserId).Distinct().ToList();
            var membersQuery = _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.IsApproved);

            membersQuery = memberIdsFromHistory.Any()
                ? membersQuery.Where(u => memberIdsFromHistory.Contains(u.Id))
                : membersQuery.Where(u => !u.IsDeleted && u.Role == SystemRoles.User && u.UnitId == unitId);

            var members = await membersQuery.ToListAsync(cancellationToken);

            if (!members.Any()) return new List<PerformanceDto>();

            var memberIds = members.Select(u => u.Id).ToList();

            var allAssignees = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.UserId.HasValue && memberIds.Contains(a.UserId.Value))
                .ToListAsync(cancellationToken);

            var allTaskIds = allAssignees.Select(a => a.TaskId).Distinct().ToList();

            var allTasks = await _taskRepo.QueryReadOnly()
                .Where(t => allTaskIds.Contains(t.Id) && !t.IsDeleted && t.UnitId == unitId)
                .Where(t => t.CreatedAt <= to && (!t.CompletedAt.HasValue || t.CompletedAt.Value >= from))
                .ToListAsync(cancellationToken);

            var relevantTaskIds = allTasks.Select(t => t.Id).ToList();

            var allProgress = await _context.Progresses
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => memberIds.Contains(p.UserId) && relevantTaskIds.Contains(p.TaskId))
                .Where(p => p.UpdatedAt >= from && p.UpdatedAt <= to)
                .ToListAsync(cancellationToken);

            var result = new List<PerformanceDto>();
            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var assignedTaskIds = allAssignees
                    .Where(a => a.UserId == member.Id)
                    .Select(a => a.TaskId)
                    .ToHashSet();

                var memberHistory = histories
                    .Where(h => h.UserId == member.Id)
                    .OrderByDescending(h => h.EffectiveFrom)
                    .FirstOrDefault();
                var memberFrom = memberHistory == null ? from : MaxDate(from, memberHistory.EffectiveFrom);
                var memberTo = memberHistory == null ? to : MinDate(to, memberHistory.EffectiveTo ?? to);

                var memberTasks = allTasks
                    .Where(task => assignedTaskIds.Contains(task.Id))
                    .Where(task =>
                        task.CreatedAt <= memberTo &&
                        (!task.CompletedAt.HasValue || task.CompletedAt.Value >= memberFrom))
                    .ToList();
                var memberTaskIds = memberTasks.Select(task => task.Id).ToHashSet();
                var memberProgress = allProgress
                    .Where(progress =>
                        progress.UserId == member.Id &&
                        memberTaskIds.Contains(progress.TaskId) &&
                        progress.UpdatedAt >= memberFrom &&
                        progress.UpdatedAt <= memberTo)
                    .ToList();

                result.Add(CalculatePersonalKpiInMemory(member.Id, member, now, memberTasks, memberProgress, period, memberFrom, memberTo, unitId));
            }

            return result;
        }

        private PerformanceDto CalculatePersonalKpiInMemory(
            Guid userId, User user, DateTime now,
            List<TaskItem> tasks,
            List<Progress> progressList,
            KpiPeriod period,
            DateTime from,
            DateTime to,
            Guid? unitId)
        {
            var metrics = CalculatePersonalKpiMetrics(tasks, progressList, now, from, to);
            var dto = BuildPersonalPerformanceDto(userId, user, metrics);

            return ApplyPeriodMetadata(dto, period, unitId, SystemRoles.User, from, to, period.Status == "Locked");
        }

        private static PerformanceDto BuildPersonalPerformanceDto(Guid userId, User user, PersonalKpiMetrics metrics)
        {
            return new PerformanceDto
            {
                UserId = userId,
                FullName = user.FullName ?? "-",
                EmployeeCode = user.EmployeeCode ?? "-",
                Score = metrics.Score,
                Level = metrics.Level,
                LevelColor = metrics.LevelColor,
                LevelIcon = metrics.LevelIcon,
                TotalTasks = metrics.TotalTasks,
                CompletedOnTime = metrics.CompletedOnTime,
                CompletedLate = metrics.CompletedLate,
                OverdueTasks = metrics.OverdueTasks,
                RejectedReports = metrics.RejectedReports,
                BonusPoints = metrics.BonusPoints,
                PenaltyPoints = metrics.PenaltyPoints,
                IsAtRisk = metrics.IsAtRisk,
                WarningMessage = metrics.WarningMessage
            };
        }

        private static PersonalKpiMetrics CalculatePersonalKpiMetrics(
            List<TaskItem> tasks,
            List<Progress> progressList,
            DateTime now,
            DateTime from,
            DateTime to)
        {
            var overdueTasks = tasks
                .Where(t => t.DueDate.HasValue
                         && GetEffectiveDeadline(t.DueDate.Value) >= from
                         && GetEffectiveDeadline(t.DueDate.Value) <= to
                         && GetEffectiveDeadline(t.DueDate.Value) < MinDate(now, to)
                         && t.Status != TaskStatusEnum.Approved
                         && t.Status != TaskStatusEnum.Submitted)
                .ToList();

            var approvedTasksWithDeadline = tasks
                .Where(t => t.Status == TaskStatusEnum.Approved
                            && t.DueDate.HasValue
                            && (t.CompletedAt.HasValue
                                ? t.CompletedAt.Value >= from && t.CompletedAt.Value <= to
                                : t.DueDate.Value >= from && t.DueDate.Value <= to))
                .ToList();

            int completedOnTime = 0;
            int completedLate = 0;
            foreach (var approvedTask in approvedTasksWithDeadline)
            {
                var lastProgress = GetLastProgress(progressList, approvedTask.Id);

                if (lastProgress != null && approvedTask.DueDate.HasValue)
                {
                    if (lastProgress.UpdatedAt <= GetEffectiveDeadline(approvedTask.DueDate.Value)) completedOnTime++;
                    else completedLate++;
                }
            }

            int rejectedCount = progressList.Count(p => p.Status == ProgressStatusEnum.Rejected);

            int bonusPoints = (int)Math.Round(approvedTasksWithDeadline
                .Where(t =>
                {
                    var lastProgress = GetLastProgress(progressList, t.Id);
                    return lastProgress != null && t.DueDate.HasValue && lastProgress.UpdatedAt <= GetEffectiveDeadline(t.DueDate.Value);
                })
                .Sum(t => 5 * GetTaskWeight(t)));

            bonusPoints += (int)Math.Round(tasks
                .Where(t => t.Status == TaskStatusEnum.Approved
                            && !t.DueDate.HasValue
                            && (!t.CompletedAt.HasValue || (t.CompletedAt.Value >= from && t.CompletedAt.Value <= to)))
                .Sum(t => 3 * GetTaskWeight(t)));

            int currentStreak = 0;
            int maxStreak = 0;
            var orderedApprovedTasks = approvedTasksWithDeadline
                .OrderBy(t => GetLastProgress(progressList, t.Id)?.UpdatedAt ?? DateTime.MaxValue)
                .ToList();

            foreach (var approvedTask in orderedApprovedTasks)
            {
                var lastProgress = GetLastProgress(progressList, approvedTask.Id);

                if (lastProgress != null && approvedTask.DueDate.HasValue)
                {
                    if (lastProgress.UpdatedAt <= GetEffectiveDeadline(approvedTask.DueDate.Value))
                    {
                        currentStreak++;
                        if (currentStreak > maxStreak) maxStreak = currentStreak;
                    }
                    else
                    {
                        currentStreak = 0;
                    }
                }
            }

            if (maxStreak >= 5) bonusPoints += 5;
            else if (maxStreak >= 3) bonusPoints += 2;

            int penaltyPoints = 0;
            for (int i = 0; i < overdueTasks.Count; i++)
                penaltyPoints += (int)Math.Round((i == 0 ? 5 : i == 1 ? 8 : 12) * GetTaskWeight(overdueTasks[i]));

            penaltyPoints += rejectedCount * 3;
            int score = Math.Clamp(100 + bonusPoints - penaltyPoints, 0, 120);
            var level = GetLevel(score, tasks.Count);

            bool isAtRisk = overdueTasks.Count >= 3 || score < 60;
            string warning = "";
            if (overdueTasks.Count >= 3)
                warning = $"Vi pham {overdueTasks.Count} lan qua han! Can cai thien ngay.";
            else if (score < 60)
                warning = "Diem hieu suat thap! Can chu y cai thien chat luong cong viec.";

            return new PersonalKpiMetrics(
                Score: score,
                Level: level.Level,
                LevelColor: level.Color,
                LevelIcon: level.Icon,
                TotalTasks: tasks.Count,
                CompletedOnTime: completedOnTime,
                CompletedLate: completedLate,
                OverdueTasks: overdueTasks.Count,
                RejectedReports: rejectedCount,
                BonusPoints: bonusPoints,
                PenaltyPoints: penaltyPoints,
                IsAtRisk: isAtRisk,
                WarningMessage: warning);
        }

        private static Progress? GetLastProgress(List<Progress> progressList, Guid taskId)
        {
            return progressList
                .Where(p => p.TaskId == taskId)
                .OrderByDescending(p => p.UpdatedAt)
                .FirstOrDefault();
        }

        private sealed record PersonalKpiMetrics(
            int Score,
            string Level,
            string LevelColor,
            string LevelIcon,
            int TotalTasks,
            int CompletedOnTime,
            int CompletedLate,
            int OverdueTasks,
            int RejectedReports,
            int BonusPoints,
            int PenaltyPoints,
            bool IsAtRisk,
            string WarningMessage);

        private sealed record BatchPersonalKpiData(
            IReadOnlyDictionary<Guid, HashSet<Guid>> TaskIdsByUser,
            IReadOnlyList<TaskItem> Tasks,
            IReadOnlyDictionary<Guid, List<Progress>> ProgressesByUser)
        {
            public static readonly BatchPersonalKpiData Empty = new(
                new Dictionary<Guid, HashSet<Guid>>(),
                Array.Empty<TaskItem>(),
                new Dictionary<Guid, List<Progress>>());
        }

        private async Task<List<UserWorkHistory>> GetOverlappingHistoriesAsync(
            User user,
            KpiPeriod period,
            CancellationToken cancellationToken)
        {
            var histories = await _context.UserWorkHistories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(h => h.UserId == user.Id
                            && h.EffectiveFrom <= period.EndDate
                            && (!h.EffectiveTo.HasValue || h.EffectiveTo.Value >= period.StartDate))
                .OrderBy(h => h.EffectiveFrom)
                .ToListAsync(cancellationToken);

            if (histories.Any()) return histories;

            return CreateFallbackHistories(user, period);
        }

        private static List<UserWorkHistory> CreateFallbackHistories(User user, KpiPeriod period)
        {
            var effectiveFrom = user.JoinedUnitAt == default ? period.StartDate : user.JoinedUnitAt;
            if (effectiveFrom > period.EndDate) return new List<UserWorkHistory>();

            return new List<UserWorkHistory>
            {
                new UserWorkHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    UnitId = user.UnitId,
                    Role = user.Role,
                    EffectiveFrom = effectiveFrom < period.StartDate ? period.StartDate : effectiveFrom
                }
            };
        }

        private PerformanceDto MergeSegmentPerformance(User user, KpiPeriod period, List<PerformanceDto> segments)
        {
            if (segments.Count == 1) return segments[0];

            var weightedHours = segments
                .Select(s => new
                {
                    Segment = s,
                    Hours = Math.Max(1, ((s.EffectiveTo ?? period.EndDate) - (s.EffectiveFrom ?? period.StartDate)).TotalHours)
                })
                .ToList();

            var totalHours = weightedHours.Sum(x => x.Hours);
            var score = (int)Math.Round(weightedHours.Sum(x => x.Segment.Score * x.Hours) / totalHours);
            var level = GetLevel(score, segments.Sum(s => s.TotalTasks));

            var latest = segments.OrderByDescending(s => s.EffectiveFrom ?? period.StartDate).First();
            return new PerformanceDto
            {
                UserId = user.Id,
                FullName = user.FullName ?? "-",
                EmployeeCode = user.EmployeeCode ?? "-",
                Score = score,
                Level = level.Level,
                LevelColor = level.Color,
                LevelIcon = level.Icon,
                TotalTasks = segments.Sum(s => s.TotalTasks),
                CompletedOnTime = segments.Sum(s => s.CompletedOnTime),
                CompletedLate = segments.Sum(s => s.CompletedLate),
                OverdueTasks = segments.Sum(s => s.OverdueTasks),
                RejectedReports = segments.Sum(s => s.RejectedReports),
                BonusPoints = segments.Sum(s => s.BonusPoints),
                PenaltyPoints = segments.Sum(s => s.PenaltyPoints),
                ReviewPenaltyPoints = segments.Sum(s => s.ReviewPenaltyPoints),
                IsManagerKpi = segments.Any(s => s.IsManagerKpi),
                UnitAverageScore = segments.Where(s => s.UnitAverageScore > 0).DefaultIfEmpty().Average(s => s?.UnitAverageScore ?? 0),
                PersonalScore = (int)Math.Round(segments.Average(s => s.PersonalScore > 0 ? s.PersonalScore : s.Score)),
                IsAtRisk = segments.Any(s => s.IsAtRisk) || score < 60,
                WarningMessage = string.Join(" | ", segments.Select(s => s.WarningMessage).Where(w => !string.IsNullOrWhiteSpace(w))),
                PeriodId = period.Id,
                PeriodName = period.Name,
                PeriodStatus = period.Status,
                UnitId = latest.UnitId,
                UnitName = latest.UnitName,
                Role = "Mixed",
                EffectiveFrom = segments.Min(s => s.EffectiveFrom) ?? period.StartDate,
                EffectiveTo = segments.Max(s => s.EffectiveTo) ?? period.EndDate,
                IsLocked = period.Status == "Locked",
                IsPartialPeriod = true,
                PeriodNote = "KPI trong ky co nhieu giai doan phong ban/chuc vu, diem duoc binh quan theo thoi gian."
            };
        }

        private PerformanceDto ApplyPeriodMetadata(PerformanceDto dto, KpiPeriod period, Guid? unitId, string role, DateTime from, DateTime to, bool isLocked)
        {
            dto.PeriodId = period.Id;
            dto.PeriodName = period.Name;
            dto.PeriodStatus = period.Status;
            dto.UnitId = unitId;
            dto.Role = role;
            dto.EffectiveFrom = from;
            dto.EffectiveTo = to;
            dto.IsLocked = isLocked;
            dto.IsPartialPeriod = from > period.StartDate || to < period.EndDate;
            dto.PeriodNote = dto.IsPartialPeriod
                ? "KPI chi tinh trong giai doan nhan su thuoc phong ban/chuc vu nay."
                : "";
            return dto;
        }

        private static PerformanceDto MapKpiResult(
            KpiResult result,
            KpiPeriod period)
        {
            var level = GetLevel(result.Score, result.TotalTasks);

            return new PerformanceDto
            {
                UserId = result.UserId,
                FullName = string.IsNullOrWhiteSpace(result.FullNameSnapshot)
                    ? "-"
                    : result.FullNameSnapshot,
                EmployeeCode = string.IsNullOrWhiteSpace(result.EmployeeCodeSnapshot)
                    ? "-"
                    : result.EmployeeCodeSnapshot,
                PeriodId = period.Id,
                PeriodName = period.Name,
                PeriodStatus = period.Status,
                UnitId = result.UnitId,
                UnitName = result.UnitNameSnapshot,
                Role = result.Role,
                EffectiveFrom = result.EffectiveFrom,
                EffectiveTo = result.EffectiveTo,
                IsLocked = true,
                IsPartialPeriod = result.EffectiveFrom > period.StartDate || result.EffectiveTo < period.EndDate,
                Score = result.Score,
                Level = string.IsNullOrWhiteSpace(result.Level) ? level.Level : result.Level,
                LevelColor = level.Color,
                LevelIcon = level.Icon,
                TotalTasks = result.TotalTasks,
                CompletedOnTime = result.CompletedOnTime,
                CompletedLate = result.CompletedLate,
                OverdueTasks = result.OverdueTasks,
                RejectedReports = result.RejectedReports,
                BonusPoints = result.BonusPoints,
                PenaltyPoints = result.PenaltyPoints,
                ReviewPenaltyPoints = result.ReviewPenaltyPoints,
                IsManagerKpi = result.IsManagerKpi,
                UnitAverageScore = result.UnitAverageScore,
                PersonalScore = result.PersonalScore,
                IsAtRisk = result.IsAtRisk,
                WarningMessage = result.WarningMessage,
                PeriodNote = "KPI da chot, khong thay doi theo du lieu moi."
            };
        }

        private static PerformanceDto CreateEmptyPerformance(Guid userId, User user)
        {
            var level = GetLevel(100, 0);
            return new PerformanceDto
            {
                UserId = userId,
                FullName = user.FullName ?? "-",
                EmployeeCode = user.EmployeeCode ?? "-",
                Score = 100,
                Level = level.Level,
                LevelColor = level.Color,
                LevelIcon = level.Icon,
                PeriodNote = "Chua co du lieu KPI trong ky nay."
            };
        }

        private static double GetTaskWeight(TaskItem task)
        {
            var priorityWeight = task.Priority switch
            {
                WorkManagementSystem.Domain.Enums.TaskPriority.Low => 0.8,
                WorkManagementSystem.Domain.Enums.TaskPriority.High => 1.25,
                WorkManagementSystem.Domain.Enums.TaskPriority.Urgent => 1.5,
                _ => 1.0
            };

            return Math.Clamp(priorityWeight, 0.5, 2.0);
        }

        private static DateTime GetEffectiveDeadline(DateTime dueDate)
            => dueDate.TimeOfDay == TimeSpan.Zero
                ? dueDate.Date.AddDays(1).AddTicks(-1)
                : dueDate;

        private static (string Level, string Color, string Icon) GetLevel(int score, int totalTasks)
        {
            if (totalTasks == 0) return ("Moi/Thu viec", "gray", "*");
            if (score >= 90) return ("Xuat sac", "green", "*");
            if (score >= 75) return ("Tot", "blue", "+");
            if (score >= 60) return ("Trung binh", "yellow", "!");
            return ("Yeu", "red", "!");
        }

        private static DateTime MaxDate(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime MinDate(DateTime a, DateTime b) => a < b ? a : b;
    }
}
