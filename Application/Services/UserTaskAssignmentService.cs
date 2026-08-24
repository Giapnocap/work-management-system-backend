using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class UserTaskAssignmentService : IUserTaskAssignmentService
    {
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IAppDbContext _context;

        public UserTaskAssignmentService(
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IAppDbContext context)
        {
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _context = context;
        }

        public async Task EnsureCanChangeAssignmentAsync(
            User user,
            string newRole,
            Guid? newUnitId,
            CancellationToken cancellationToken = default)
        {
            var unitChanged = user.UnitId != newUnitId;
            var roleChanged = user.Role != newRole;
            if (!unitChanged && !roleChanged)
                return;

            if (user.Role == SystemRoles.Manager)
            {
                var managedTasks = await GetPendingManagedTasksAsync(user, cancellationToken);
                ThrowIfPending(
                    managedTasks,
                    "Không thể thay đổi Trưởng phòng khi phòng ban còn công việc chưa hoàn thành");
                return;
            }

            if (unitChanged || newRole == SystemRoles.Manager)
            {
                var assignedTasks = await GetPendingAssignedTasksAsync(user.Id, cancellationToken);
                ThrowIfPending(
                    assignedTasks,
                    "Không thể luân chuyển hoặc bổ nhiệm khi nhân sự còn công việc chưa hoàn thành");
                await EnsureNoRecurringAssignmentAsync(user.Id, cancellationToken);
            }
        }

        public async Task EnsureCanDeleteAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            if (user.Role == SystemRoles.Admin)
                throw new BusinessException("Không thể xóa tài khoản Admin!");

            var pendingTasks = user.Role == SystemRoles.Manager
                ? await GetPendingManagedTasksAsync(user, cancellationToken)
                : await GetPendingAssignedTasksAsync(user.Id, cancellationToken);

            ThrowIfPending(
                pendingTasks,
                "Không thể xóa nhân sự khi vẫn còn trách nhiệm công việc");
            await EnsureNoRecurringAssignmentAsync(user.Id, cancellationToken);
        }

        private async Task<List<string>> GetPendingAssignedTasksAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return await _taskRepo.QueryReadOnly()
                .Where(task => !task.IsDeleted && task.Status != TaskStatusEnum.Approved)
                .Join(
                    _assigneeRepo.QueryReadOnly().Where(assignee => assignee.UserId == userId),
                    task => task.Id,
                    assignee => assignee.TaskId,
                    (task, _) => task.Title)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private async Task<List<string>> GetPendingManagedTasksAsync(
            User manager,
            CancellationToken cancellationToken)
        {
            return await _taskRepo.QueryReadOnly()
                .Where(task =>
                    !task.IsDeleted &&
                    task.Status != TaskStatusEnum.Approved &&
                    (task.CreatedBy == manager.Id ||
                     (manager.UnitId.HasValue && task.UnitId == manager.UnitId)))
                .Select(task => task.Title)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private static void ThrowIfPending(IReadOnlyCollection<string> taskTitles, string message)
        {
            if (taskTitles.Count == 0)
                return;

            throw new BusinessException(
                $"{message}. Còn {taskTitles.Count} công việc: {string.Join(", ", taskTitles)}. " +
                "Vui lòng hoàn thành hoặc bàn giao công việc trước.");
        }

        private async Task EnsureNoRecurringAssignmentAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var templateTitles = await _context.RecurringTaskAssignees
                .AsNoTracking()
                .Where(assignee => assignee.UserId == userId)
                .Join(
                    _context.RecurringTaskTemplates.AsNoTracking(),
                    assignee => assignee.TemplateId,
                    template => template.Id,
                    (_, template) => template.Title)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (templateTitles.Count == 0)
                return;

            throw new BusinessException(
                $"Không thể điều chuyển hoặc xóa nhân sự đang là người nhận mặc định của " +
                $"{templateTitles.Count} lịch công việc định kỳ: {string.Join(", ", templateTitles)}. " +
                "Hãy cập nhật hoặc xóa các lịch trước.");
        }
    }
}
