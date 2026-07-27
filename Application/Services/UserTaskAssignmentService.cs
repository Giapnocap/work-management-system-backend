using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class UserTaskAssignmentService : IUserTaskAssignmentService
    {
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;

        public UserTaskAssignmentService(
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo)
        {
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
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

            if (user.Role == "Manager")
            {
                var managedTasks = await GetPendingManagedTasksAsync(user, cancellationToken);
                ThrowIfPending(
                    managedTasks,
                    "Khong the thay doi Truong phong khi phong ban con cong viec chua hoan thanh");
                return;
            }

            if (unitChanged || newRole == "Manager")
            {
                var assignedTasks = await GetPendingAssignedTasksAsync(user.Id, cancellationToken);
                ThrowIfPending(
                    assignedTasks,
                    "Khong the luan chuyen hoac bo nhiem khi nhan su con cong viec chua hoan thanh");
            }
        }

        public async Task EnsureCanDeleteAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            if (user.Role == "Admin")
                throw new BusinessException("Khong the xoa tai khoan Admin!");

            var pendingTasks = user.Role == "Manager"
                ? await GetPendingManagedTasksAsync(user, cancellationToken)
                : await GetPendingAssignedTasksAsync(user.Id, cancellationToken);

            ThrowIfPending(
                pendingTasks,
                "Khong the xoa nhan su khi van con trach nhiem cong viec");
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
                $"{message}. Con {taskTitles.Count} cong viec: {string.Join(", ", taskTitles)}. " +
                "Vui long hoan thanh hoac ban giao cong viec truoc.");
        }
    }
}
