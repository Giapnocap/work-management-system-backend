using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class TaskBusinessRuleService : ITaskBusinessRuleService
    {
        private readonly IGenericRepository<User> _userRepo;
        private readonly AppDbContext _context;

        public TaskBusinessRuleService(IGenericRepository<User> userRepo, AppDbContext context)
        {
            _userRepo = userRepo;
            _context = context;
        }

        public async Task<Project?> ValidateProjectScope(
            Guid? projectId,
            Guid taskUnitId,
            User manager,
            CancellationToken cancellationToken = default)
        {
            if (!manager.UnitId.HasValue)
                throw new ForbiddenException("Manager chua thuoc phong ban nao.");

            if (manager.UnitId.Value != taskUnitId)
                throw new ForbiddenException("Manager chi duoc quan ly cong viec trong phong ban hien tai.");

            if (!projectId.HasValue)
                return null;

            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == projectId.Value, cancellationToken)
                ?? throw new NotFoundException("Project not found.");

            if (project.IsArchived)
                throw new BusinessException("Project da duoc luu tru, khong the gan hoac mo lai cong viec.");

            if (project.UnitId != taskUnitId)
                throw new ForbiddenException("Task va project phai thuoc cung mot phong ban.");

            return project;
        }

        public async Task<TaskAssignmentPlan> ResolveAssignmentPlan(
            IEnumerable<Guid> directUserIds,
            IEnumerable<Guid> unitIds,
            Guid managerUnitId,
            CancellationToken cancellationToken = default)
        {
            var selectedUserIds = directUserIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var selectedUnitIds = unitIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            selectedUnitIds = selectedUnitIds.Any()
                ? selectedUnitIds
                : new List<Guid> { managerUnitId };

            if (selectedUnitIds.Any(id => id != managerUnitId))
                throw new ForbiddenException("Manager chi duoc giao viec trong phong ban cua minh.");

            var userIds = selectedUserIds.Any()
                ? await ResolveDirectAssigneeUserIds(selectedUserIds, managerUnitId, cancellationToken)
                : await ResolveUnitSnapshotUserIds(selectedUnitIds, cancellationToken);

            if (!userIds.Any())
                throw new BusinessException("Khong co nhan vien phu hop de giao cong viec.");

            return new TaskAssignmentPlan(userIds, !selectedUserIds.Any());
        }

        public void EnsureCanEdit(TaskItem task)
        {
            if (task.Status == WorkManagementSystem.Domain.Enums.TaskStatus.Approved)
                throw new BusinessException("Cong viec da hoan thanh, khong the chinh sua.");
        }

        public async Task EnsureCanDelete(
            TaskItem task,
            CancellationToken cancellationToken = default)
        {
            if (task.Status != WorkManagementSystem.Domain.Enums.TaskStatus.NotStarted ||
                task.CompletedAt.HasValue ||
                task.CompletedBy.HasValue ||
                task.ActualHours > 0)
            {
                throw new BusinessException(
                    "Chi co the xoa cong viec chua bat dau va chua co du lieu thuc thi.");
            }

            var hasProgress = await _context.Progresses
                .IgnoreQueryFilters()
                .AnyAsync(progress => progress.TaskId == task.Id, cancellationToken);
            if (hasProgress)
                throw new BusinessException("Khong the xoa cong viec da co bao cao tien do.");

            var hasUpload = await _context.UploadFiles
                .IgnoreQueryFilters()
                .AnyAsync(file => file.TaskId == task.Id, cancellationToken);
            if (hasUpload)
                throw new BusinessException("Khong the xoa cong viec da co tep dinh kem.");

            var hasComment = await _context.TaskComments
                .IgnoreQueryFilters()
                .AnyAsync(comment => comment.TaskId == task.Id, cancellationToken);
            if (hasComment)
                throw new BusinessException("Khong the xoa cong viec da co trao doi.");

            var hasSubTask = await _context.SubTasks
                .IgnoreQueryFilters()
                .AnyAsync(subTask => subTask.TaskId == task.Id, cancellationToken);
            if (hasSubTask)
                throw new BusinessException("Khong the xoa cong viec da co cong viec con.");
        }

        private async Task<List<Guid>> ResolveDirectAssigneeUserIds(
            List<Guid> directUserIds,
            Guid managerUnitId,
            CancellationToken cancellationToken)
        {
            var assignableUserIds = await _userRepo.QueryReadOnly()
                .Where(u => directUserIds.Contains(u.Id) &&
                            u.UnitId == managerUnitId &&
                            u.Role == "User" &&
                            u.IsApproved &&
                            !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var invalidUserIds = directUserIds.Except(assignableUserIds).ToList();
            if (invalidUserIds.Any())
                throw new ForbiddenException("Chi duoc giao viec cho nhan vien dang hoat dong trong phong ban cua ban.");

            return assignableUserIds.Distinct().ToList();
        }

        private async Task<List<Guid>> ResolveUnitSnapshotUserIds(
            List<Guid> unitIds,
            CancellationToken cancellationToken)
        {
            return await _userRepo.QueryReadOnly()
                .Where(u => u.UnitId.HasValue &&
                            unitIds.Contains(u.UnitId.Value) &&
                            u.Role == "User" &&
                            u.IsApproved &&
                            !u.IsDeleted)
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}
