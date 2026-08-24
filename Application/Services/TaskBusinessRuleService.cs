using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class TaskBusinessRuleService : ITaskBusinessRuleService
    {
        private readonly IGenericRepository<User> _userRepo;
        private readonly IAppDbContext _context;

        public TaskBusinessRuleService(IGenericRepository<User> userRepo, IAppDbContext context)
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
                throw new ForbiddenException("Trưởng phòng chưa thuộc phòng ban nào.");

            if (manager.UnitId.Value != taskUnitId)
                throw new ForbiddenException("Trưởng phòng chỉ được quản lý công việc trong phòng ban hiện tại.");

            if (!projectId.HasValue)
                return null;

            var project = await _context.Projects
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy dự án.");

            if (project.IsArchived)
                throw new BusinessException("Dự án đã được lưu trữ, không thể gắn hoặc mở lại công việc.");

            if (project.UnitId != taskUnitId)
                throw new ForbiddenException("Công việc và dự án phải thuộc cùng một phòng ban.");

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
                throw new ForbiddenException("Trưởng phòng chỉ được giao việc trong phòng ban của mình.");

            var userIds = selectedUserIds.Any()
                ? await ResolveDirectAssigneeUserIds(selectedUserIds, managerUnitId, cancellationToken)
                : await ResolveUnitSnapshotUserIds(selectedUnitIds, cancellationToken);

            if (!userIds.Any())
                throw new BusinessException("Không có nhân viên phù hợp để giao công việc.");

            return new TaskAssignmentPlan(userIds, !selectedUserIds.Any());
        }

        public void EnsureCanEdit(TaskItem task)
        {
            if (task.Status == WorkManagementSystem.Domain.Enums.TaskStatus.Approved)
                throw new BusinessException("Công việc đã hoàn thành, không thể chỉnh sửa.");
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
                    "Chỉ có thể xóa công việc chưa bắt đầu và chưa có dữ liệu thực thi.");
            }

            var hasProgress = await _context.Progresses
                .IgnoreQueryFilters()
                .AnyAsync(progress => progress.TaskId == task.Id, cancellationToken);
            if (hasProgress)
                throw new BusinessException("Không thể xóa công việc đã có báo cáo tiến độ.");

            var hasUpload = await _context.UploadFiles
                .IgnoreQueryFilters()
                .AnyAsync(file => file.TaskId == task.Id, cancellationToken);
            if (hasUpload)
                throw new BusinessException("Không thể xóa công việc đã có tệp đính kèm.");

            var hasComment = await _context.TaskComments
                .IgnoreQueryFilters()
                .AnyAsync(comment => comment.TaskId == task.Id, cancellationToken);
            if (hasComment)
                throw new BusinessException("Không thể xóa công việc đã có trao đổi.");

            var hasSubTask = await _context.SubTasks
                .IgnoreQueryFilters()
                .AnyAsync(subTask => subTask.TaskId == task.Id, cancellationToken);
            if (hasSubTask)
                throw new BusinessException("Không thể xóa công việc đã có công việc con.");

            var hasDependency = await _context.TaskDependencies.AnyAsync(
                dependency => dependency.TaskId == task.Id ||
                              dependency.DependsOnTaskId == task.Id,
                cancellationToken);
            if (hasDependency)
            {
                throw new BusinessException(
                    "Không thể xóa công việc đang có quan hệ phụ thuộc. Hãy gỡ quan hệ phụ thuộc trước.");
            }
        }

        private async Task<List<Guid>> ResolveDirectAssigneeUserIds(
            List<Guid> directUserIds,
            Guid managerUnitId,
            CancellationToken cancellationToken)
        {
            var assignableUserIds = await _userRepo.QueryReadOnly()
                .Where(u => directUserIds.Contains(u.Id) &&
                            u.UnitId == managerUnitId &&
                    u.Role == SystemRoles.User &&
                            u.IsApproved &&
                            !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var invalidUserIds = directUserIds.Except(assignableUserIds).ToList();
            if (invalidUserIds.Any())
                throw new ForbiddenException("Chỉ được giao việc cho nhân viên đang hoạt động trong phòng ban của bạn.");

            return assignableUserIds.Distinct().ToList();
        }

        private async Task<List<Guid>> ResolveUnitSnapshotUserIds(
            List<Guid> unitIds,
            CancellationToken cancellationToken)
        {
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
    }
}
