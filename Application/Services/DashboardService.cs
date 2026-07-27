using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;
using ProgressStatus = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboard(CancellationToken cancellationToken = default)
        {
            var tasks = await _context.Tasks.AsNoTracking().ToListAsync(cancellationToken);
            var units = await _context.Units.AsNoTracking().ToListAsync(cancellationToken);

            return new DashboardDto
            {
                TotalTasks = tasks.Count,
                TotalUsers = await _context.Users.CountAsync(cancellationToken),
                TotalUnits = units.Count,

                TaskPending = tasks.Count(t => t.Status == TaskStatus.NotStarted),
                TaskInProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
                TaskApproved = tasks.Count(t => t.Status == TaskStatus.Approved),
                RejectedReports = await _context.Progresses
                    .CountAsync(progress => progress.Status == ProgressStatus.Rejected, cancellationToken),
                ReportSubmitted = tasks.Count(t => t.Status == TaskStatus.Submitted),

                UnitSummaries = units.Select((u, index) => new UnitSummaryDto
                {
                    UnitName = u.Name,
                    UnitCode = $"UNIT-{(index + 1):D2}",
                    TotalTasks = tasks.Count(t => t.UnitId == u.Id),
                    ApprovedTasks = tasks.Count(t => t.UnitId == u.Id && t.Status == TaskStatus.Approved)
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

            var members = await _context.Users
                .Where(u => u.UnitId == unitId.Value && u.Role != "Manager" && u.IsApproved && !u.IsDeleted)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var memberIds = members.Select(m => m.Id).ToList();

            var taskIds = await _context.TaskAssignees
                .Where(ta => ta.UserId.HasValue && memberIds.Contains(ta.UserId.Value))
                .Select(ta => ta.TaskId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var tasks = await _context.Tasks
                .Where(t => t.UnitId == unitId.Value && taskIds.Contains(t.Id))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var scopedTaskIds = tasks.Select(task => task.Id).ToList();

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
