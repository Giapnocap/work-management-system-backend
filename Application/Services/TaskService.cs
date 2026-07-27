using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;
using TaskPriorityEnum = WorkManagementSystem.Domain.Enums.TaskPriority;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskHistory> _historyRepo;
        private readonly INotificationService _notificationService;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskWorkflowService _workflowService;
        private readonly ITaskBusinessRuleService _taskRules;
        private readonly ITaskDtoBuilder _taskDtoBuilder;
        private readonly ITransactionManager _transactionManager;

        public TaskService(
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskHistory> historyRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            ITaskBusinessRuleService taskRules,
            ITaskDtoBuilder taskDtoBuilder,
            ITransactionManager transactionManager)
        {
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _userRepo = userRepo;
            _historyRepo = historyRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _taskRules = taskRules;
            _taskDtoBuilder = taskDtoBuilder;
            _transactionManager = transactionManager;
        }

        public async Task<Guid?> GetManagerUnitId(Guid managerId, CancellationToken cancellationToken = default)
        {
            var manager = await _userRepo.GetByIdAsync(managerId, cancellationToken);
            return manager?.UnitId;
        }

        public Task<TaskDto> Create(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => CreateCore(dto, userId, token),
                cancellationToken);

        private async Task<TaskDto> CreateCore(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken)
        {
            var creator = await GetManagerOrThrow(userId, "tao cong viec", cancellationToken);

            if (!creator.UnitId.HasValue)
                throw new BusinessException("Manager chua thuoc phong ban nao.");

            ValidateDateRange(dto.StartDate, dto.DueDate);
            var project = await _taskRules.ValidateProjectScope(
                dto.ProjectId,
                creator.UnitId.Value,
                creator,
                cancellationToken);

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                RequiresReview = dto.RequiresReview,
                Priority = ParsePriority(dto.Priority),
                UnitId = creator.UnitId,
                ProjectId = project?.Id
            };

            var assignmentPlan = await _taskRules.ResolveAssignmentPlan(
                dto.UserIds,
                dto.UnitIds,
                creator.UnitId.Value,
                cancellationToken);

            await _taskRepo.AddAsync(task, cancellationToken);
            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = userId,
                FieldName = "Created",
                NewValue = task.Title
            }, cancellationToken);

            foreach (var assigneeId in assignmentPlan.UserIds)
            {
                await _assigneeRepo.AddAsync(new TaskAssignee
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = assigneeId
                }, cancellationToken);

                var message = assignmentPlan.IsDepartmentAssignment
                    ? $"Phong ban cua ban vua duoc giao cong viec moi: {task.Title}"
                    : $"Ban vua duoc giao cong viec moi: {task.Title}";
                await _notificationService.AddNotification(assigneeId, message, cancellationToken);
            }

            await _taskRepo.SaveAsync(cancellationToken);
            return await _taskDtoBuilder.BuildTaskDto(task, cancellationToken);
        }

        public async Task<object> Get(
            string keyword,
            int page,
            int size,
            string? status,
            Guid currentUserId,
            Guid? userId = null,
            Guid? unitId = null,
            Guid? projectId = null,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size);
            page = paging.Page;
            size = paging.Size;

            var requester = await _userRepo.GetByIdAsync(currentUserId, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var query = _taskRepo.QueryReadOnly().Where(t => !t.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(t => t.Title.Contains(keyword.Trim()));

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TaskStatusEnum>(status, true, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (requester.Role == "Admin")
            {
                if (unitId.HasValue)
                    query = query.Where(t => t.UnitId == unitId.Value);
                if (userId.HasValue)
                    query = query.Where(t => _assigneeRepo.QueryReadOnly().Any(a => a.TaskId == t.Id && a.UserId == userId.Value));
            }
            else if (requester.Role == "Manager")
            {
                if (!requester.UnitId.HasValue)
                    query = query.Where(t => false);
                else
                {
                    var managerUnitId = requester.UnitId.Value;
                    query = query.Where(t => t.UnitId == managerUnitId ||
                                             _assigneeRepo.QueryReadOnly().Any(a => a.TaskId == t.Id && a.UnitId == managerUnitId));

                    if (userId.HasValue)
                        query = query.Where(t => _assigneeRepo.QueryReadOnly().Any(a => a.TaskId == t.Id && a.UserId == userId.Value));
                }
            }
            else
            {
                var accessibleTaskIds = await GetAccessibleTaskIdsForUser(requester, cancellationToken);
                query = query.Where(t => accessibleTaskIds.Contains(t.Id));
            }

            var total = await query.CountAsync(cancellationToken);
            var tasks = await query
                .OrderBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await _taskDtoBuilder.BuildTaskDtos(tasks, cancellationToken);
            return new { total, page, size, data = dtos };
        }

        public Task<TaskDto> Update(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => UpdateCore(id, dto, changedBy, token),
                cancellationToken);

        private async Task<TaskDto> UpdateCore(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            _taskRules.EnsureCanEdit(task);

            var changer = await GetManagerOrThrow(changedBy, "chinh sua cong viec", cancellationToken);
            await EnsureTaskAccess(
                id,
                changedBy,
                true,
                "Ban khong co quyen chinh sua cong viec nay.",
                cancellationToken);

            ValidateDateRange(dto.StartDate, dto.DueDate);
            if (!task.UnitId.HasValue)
                throw new BusinessException("Cong viec khong co phong ban hop le.");

            var project = await _taskRules.ValidateProjectScope(
                dto.ProjectId,
                task.UnitId.Value,
                changer,
                cancellationToken);

            var newTitle = dto.Title.Trim();
            var newDescription = dto.Description?.Trim() ?? string.Empty;
            var newPriority = ParsePriority(dto.Priority);

            await AddHistoryIfChanged(task, changedBy, "Title", task.Title, newTitle, cancellationToken);
            await AddHistoryIfChanged(task, changedBy, "Description", task.Description, newDescription, cancellationToken);
            await AddHistoryIfChanged(task, changedBy, "StartDate", task.StartDate?.ToString("yyyy-MM-dd"), dto.StartDate?.ToString("yyyy-MM-dd"), cancellationToken);
            await AddHistoryIfChanged(task, changedBy, "DueDate", task.DueDate?.ToString("yyyy-MM-dd"), dto.DueDate?.ToString("yyyy-MM-dd"), cancellationToken);
            await AddHistoryIfChanged(task, changedBy, "RequiresReview", task.RequiresReview.ToString(), dto.RequiresReview.ToString(), cancellationToken);
            await AddHistoryIfChanged(task, changedBy, "Priority", task.Priority.ToString(), newPriority.ToString(), cancellationToken);
            await AddHistoryIfChanged(
                task,
                changedBy,
                "ProjectId",
                task.ProjectId?.ToString(),
                project?.Id.ToString(),
                cancellationToken);

            task.Title = newTitle;
            task.Description = newDescription;
            task.StartDate = dto.StartDate;
            task.DueDate = dto.DueDate;
            task.RequiresReview = dto.RequiresReview;
            task.Priority = newPriority;
            task.ProjectId = project?.Id;

            _taskRepo.Update(task);

            var assignedUserIds = await _workflowService.ResolveTaskRecipients(task.Id, cancellationToken);
            foreach (var uid in assignedUserIds.Where(uid => uid != changedBy))
                await _notificationService.AddNotification(uid, $"Cong viec '{task.Title}' da duoc cap nhat.", cancellationToken);

            await _taskRepo.SaveAsync(cancellationToken);
            return await _taskDtoBuilder.BuildTaskDto(task, cancellationToken);
        }

        public async Task Delete(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            await _transactionManager.ExecuteSerializableAsync(async token =>
            {
                await DeleteCore(id, userId, token);
                return true;
            }, cancellationToken);
        }

        private async Task DeleteCore(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            await GetManagerOrThrow(userId, "xoa cong viec", cancellationToken);
            await EnsureTaskAccess(id, userId, true, "Ban khong co quyen xoa cong viec nay.", cancellationToken);
            await _taskRules.EnsureCanDelete(task, cancellationToken);

            task.IsDeleted = true;
            _taskRepo.Update(task);
            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = userId,
                FieldName = "IsDeleted",
                OldValue = bool.FalseString,
                NewValue = bool.TrueString
            }, cancellationToken);
            await _taskRepo.SaveAsync(cancellationToken);
        }

        public Task RemindTask(Guid taskId, Guid reminderId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteAsync(token => RemindTaskCore(taskId, reminderId, token), cancellationToken);

        private async Task RemindTaskCore(Guid taskId, Guid reminderId, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            if (task.Status == TaskStatusEnum.Approved)
                throw new BusinessException("Cong viec da hoan thanh, khong can don doc.");

            await GetManagerOrThrow(reminderId, "don doc cong viec", cancellationToken);
            await EnsureTaskAccess(taskId, reminderId, true, "Ban khong co quyen don doc cong viec nay.", cancellationToken);

            var recipients = await _workflowService.ResolveTaskRecipients(taskId, cancellationToken);
            if (!recipients.Any())
                throw new BusinessException("Khong co nhan vien nao duoc giao cong viec nay.");

            var reminder = await _userRepo.GetByIdAsync(reminderId, cancellationToken);
            foreach (var uid in recipients)
                await _notificationService.AddNotification(
                    uid,
                    $"{reminder?.FullName ?? "Quan ly"} da nhac ban ve cong viec: {task.Title}",
                    cancellationToken);

            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                ChangedBy = reminderId,
                FieldName = "Remind",
                OldValue = string.Empty,
                NewValue = "Da gui nhac nho tien do"
            }, cancellationToken);
            await _historyRepo.SaveAsync(cancellationToken);
        }

        public async Task<TaskDto> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            var task = await _taskRepo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            await EnsureTaskAccess(id, userId, false, "Ban khong co quyen xem cong viec nay.", cancellationToken);

            return await _taskDtoBuilder.BuildTaskDto(task, cancellationToken);
        }

        public async Task<List<TaskHistory>> GetHistoryAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        {
            await EnsureTaskAccess(taskId, userId, false, "Ban khong co quyen xem lich su cong viec nay.", cancellationToken);

            return await _historyRepo.QueryReadOnly()
                .Where(h => h.TaskId == taskId)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync(cancellationToken);
        }

        private async Task<User> GetManagerOrThrow(Guid userId, string action, CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (user.Role != "Manager")
                throw new ForbiddenException($"Chi Manager moi duoc {action}.");

            return user;
        }

        private async Task EnsureTaskAccess(
            Guid taskId,
            Guid userId,
            bool managerOrCreatorOnly,
            string message,
            CancellationToken cancellationToken)
        {
            if (!await _accessService.CanAccessTask(taskId, userId, managerOrCreatorOnly, cancellationToken))
                throw new ForbiddenException(message);
        }

        private async Task<List<Guid>> GetAccessibleTaskIdsForUser(User user, CancellationToken cancellationToken)
        {
            var directTaskIds = await _assigneeRepo.QueryReadOnly()
                .Where(a => a.UserId == user.Id)
                .Select(a => a.TaskId)
                .ToListAsync(cancellationToken);

            if (user.UnitId.HasValue)
            {
                var unitId = user.UnitId.Value;
                var unitTaskIds = await _assigneeRepo.QueryReadOnly()
                    .Where(a => a.UnitId == unitId &&
                                !_assigneeRepo.QueryReadOnly().Any(d => d.TaskId == a.TaskId && d.UserId.HasValue))
                    .Select(a => a.TaskId)
                    .ToListAsync(cancellationToken);

                return directTaskIds.Union(unitTaskIds).Distinct().ToList();
            }

            return directTaskIds.Distinct().ToList();
        }

        private async Task AddHistoryIfChanged(
            TaskItem task,
            Guid changedBy,
            string field,
            string? oldValue,
            string? newValue,
            CancellationToken cancellationToken)
        {
            if (oldValue == newValue) return;

            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ChangedBy = changedBy,
                FieldName = field,
                OldValue = oldValue ?? string.Empty,
                NewValue = newValue ?? string.Empty
            }, cancellationToken);
        }

        private static TaskPriorityEnum ParsePriority(string? priority)
        {
            return Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsed)
                ? parsed
                : TaskPriorityEnum.Medium;
        }

        private static void ValidateDateRange(DateTime? startDate, DateTime? dueDate)
        {
            if (startDate.HasValue && dueDate.HasValue && dueDate.Value < startDate.Value)
                throw new BusinessException("Deadline khong duoc som hon ngay bat dau.");
        }

    }
}
