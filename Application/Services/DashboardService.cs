using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using ProgressStatus = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IAppDbContext _context;

        public DashboardService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboard(CancellationToken cancellationToken = default)
        {
            var taskCounts = await _context.Tasks
                .AsNoTracking()
                .GroupBy(task => task.UnitId)
                .Select(group => new
                {
                    UnitId = group.Key,
                    Total = group.Count(),
                    Pending = group.Count(task => task.Status == TaskStatus.NotStarted),
                    InProgress = group.Count(task => task.Status == TaskStatus.InProgress),
                    Submitted = group.Count(task => task.Status == TaskStatus.Submitted),
                    Approved = group.Count(task => task.Status == TaskStatus.Approved)
                })
                .ToListAsync(cancellationToken);

            var units = await _context.Units
                .AsNoTracking()
                .Select(unit => new { unit.Id, unit.Name })
                .ToListAsync(cancellationToken);

            var taskCountsByUnit = taskCounts
                .Where(count => count.UnitId.HasValue)
                .ToDictionary(count => count.UnitId!.Value);

            return new DashboardDto
            {
                TotalTasks = taskCounts.Sum(count => count.Total),
                TotalUsers = await _context.Users.CountAsync(cancellationToken),
                TotalUnits = units.Count,

                TaskPending = taskCounts.Sum(count => count.Pending),
                TaskInProgress = taskCounts.Sum(count => count.InProgress),
                TaskApproved = taskCounts.Sum(count => count.Approved),
                RejectedReports = await _context.Progresses
                    .CountAsync(progress => progress.Status == ProgressStatus.Rejected, cancellationToken),
                ReportSubmitted = taskCounts.Sum(count => count.Submitted),

                UnitSummaries = units.Select((u, index) => new UnitSummaryDto
                {
                    UnitName = u.Name,
                    UnitCode = $"UNIT-{(index + 1):D2}",
                    TotalTasks = taskCountsByUnit.TryGetValue(u.Id, out var count) ? count.Total : 0,
                    ApprovedTasks = taskCountsByUnit.TryGetValue(u.Id, out count) ? count.Approved : 0
                }).ToList()
            };
        }

        public async Task<ManagerDashboardDto> GetManagerDashboard(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var manager = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsApproved && !u.IsDeleted, cancellationToken);
            var unitId = manager?.UnitId;
            if (!unitId.HasValue)
            {
                unitId = await _context.UserUnits
                    .Where(uu => uu.UserId == userId)
                    .Select(uu => (Guid?)uu.UnitId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (!unitId.HasValue)
                return new ManagerDashboardDto { UnitName = "Chưa có phòng ban" };

            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == unitId.Value, cancellationToken);

            var memberQuery = _context.Users
                .Where(u => u.UnitId == unitId.Value && u.Role != SystemRoles.Manager && u.IsApproved && !u.IsDeleted)
                .AsNoTracking();

            var memberIds = memberQuery.Select(member => member.Id);
            var members = await memberQuery.ToListAsync(cancellationToken);

            var taskIds = _context.TaskAssignees
                .Where(ta => ta.UserId.HasValue && memberIds.Contains(ta.UserId.Value))
                .Select(ta => ta.TaskId)
                .Distinct();

            var taskQuery = _context.Tasks
                .Where(t => t.UnitId == unitId.Value && taskIds.Contains(t.Id))
                .AsNoTracking();
            var scopedTaskIds = taskQuery.Select(task => task.Id);
            var tasks = await taskQuery.ToListAsync(cancellationToken);

            var assignees = await _context.TaskAssignees
                .Where(a => a.UserId.HasValue && memberIds.Contains(a.UserId.Value))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var memberProgresses = members.Select(m =>
            {
                var myTaskIds = assignees.Where(a => a.UserId == m.Id).Select(a => a.TaskId).Distinct().ToList();
                var myTasks = tasks.Where(t => myTaskIds.Contains(t.Id)).ToList();

                return new MemberProgressDto
                {
                    FullName = m.FullName,
                    UserEmployeeCode = m.EmployeeCode,
                    TotalTasks = myTasks.Count,
                    ApprovedTasks = myTasks.Count(t => t.Status == TaskStatus.Approved),
                    SubmittedTasks = myTasks.Count(t => t.Status == TaskStatus.Submitted)
                };
            }).ToList();

            return new ManagerDashboardDto
            {
                UnitName = unit?.Name ?? "",
                TotalMembers = members.Count,
                TotalTasks = tasks.Count,

                TaskPending = tasks.Count(t => t.Status == TaskStatus.NotStarted),
                TaskInProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
                TaskApproved = tasks.Count(t => t.Status == TaskStatus.Approved),
                RejectedReports = await _context.Progresses
                    .CountAsync(
                        progress => scopedTaskIds.Contains(progress.TaskId) &&
                                    progress.Status == ProgressStatus.Rejected,
                        cancellationToken),
                ReportSubmitted = tasks.Count(t => t.Status == TaskStatus.Submitted),

                MemberProgresses = memberProgresses
            };
        }
    }
}
