using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class TaskWorkflowService : ITaskWorkflowService
    {
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Progress> _progressRepo;

        public TaskWorkflowService(
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<Progress> progressRepo)
        {
            _assigneeRepo = assigneeRepo;
            _userRepo = userRepo;
            _progressRepo = progressRepo;
        }

        public async Task<List<Guid>> ResolveTaskRecipients(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            var directUserIds = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.TaskId == taskId && a.UserId.HasValue)
                .Select(a => a.UserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (directUserIds.Any())
                return directUserIds;

            var unitIds = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.TaskId == taskId && a.UnitId.HasValue)
                .Select(a => a.UnitId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!unitIds.Any())
                return new List<Guid>();

            return await _userRepo.QueryReadOnly()
                .Where(u => u.UnitId.HasValue &&
                            unitIds.Contains(u.UnitId.Value) &&
                    u.Role == SystemRoles.User &&
                            u.IsApproved &&
                            !u.IsDeleted)
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public Task<List<Guid>> GetExpectedUserIds(Guid taskId, CancellationToken cancellationToken = default)
            => ResolveTaskRecipients(taskId, cancellationToken);

        public async Task ApplyCompletionStateAsync(
            TaskItem task,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            var expectedUserIds = await GetExpectedUserIds(task.Id, cancellationToken);
            if (!expectedUserIds.Any())
                expectedUserIds.Add(currentUserId);

            var approvedUserIds = await _progressRepo.QueryReadOnly()
                .Where(p => p.TaskId == task.Id &&
                            p.Status == ProgressStatusEnum.Approved &&
                            p.Percent >= 100)
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            approvedUserIds.Add(currentUserId);

            if (expectedUserIds.All(id => approvedUserIds.Contains(id)))
            {
                task.Status = TaskStatusEnum.Approved;
                task.CompletedAt = DateTime.UtcNow;
                task.CompletedBy = currentUserId;
            }
            else if (task.Status != TaskStatusEnum.Approved)
            {
                task.Status = TaskStatusEnum.InProgress;
            }
        }
    }
}
