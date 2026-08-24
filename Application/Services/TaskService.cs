using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using System.Globalization;
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
        private readonly IWorkloadService _workloadService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAppDbContext _context;

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
            IWorkloadService workloadService,
            ITransactionManager transactionManager,
            IAppDbContext context)
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
            _workloadService = workloadService;
            _transactionManager = transactionManager;
            _context = context;
        }

        public Task<TaskDto> Create(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => CreateCore(dto, userId, token),
                cancellationToken);

        private async Task<TaskDto> CreateCore(CreateTaskDto dto, Guid userId, CancellationToken cancellationToken)
        {
            var creator = await GetManagerOrThrow(userId, "tạo công việc", cancellationToken);

            if (!creator.UnitId.HasValue)
                throw new BusinessException("Trưởng phòng chưa thuộc phòng ban nào.");

            ValidateDateRange(dto.StartDate, dto.DueDate);
            ValidatePlannedEffort(dto.PlannedEffortHours);
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
                PlannedEffortHours = dto.PlannedEffortHours,
                Priority = ParsePriority(dto.Priority),
                UnitId = creator.UnitId,
                ProjectId = project?.Id
            };

            var assignmentPlan = await _taskRules.ResolveAssignmentPlan(
                dto.UserIds,
                dto.UnitIds,
                creator.UnitId.Value,
                cancellationToken);

            AssignmentPreviewDto? workloadPreview = null;
            if (task.PlannedEffortHours.HasValue)
            {
                workloadPreview = await _workloadService.PreviewResolvedAssignmentAsync(
                    userId,
                    assignmentPlan.UserIds,
                    task.PlannedEffortHours.Value,
                    task.StartDate,
                    task.DueDate,
                    cancellationToken);
            }

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
                    ? $"Phòng ban của bạn vừa được giao công việc mới: {task.Title}"
                    : $"Bạn vừa được giao công việc mới: {task.Title}";
                await _notificationService.AddNotification(assigneeId, message, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            var result = await _taskDtoBuilder.BuildTaskDto(task, cancellationToken);
            result.WorkloadWarnings = workloadPreview?.Assignees
                .Where(assignee => assignee.HasWarning)
                .ToList() ?? new List<AssignmentWorkloadDto>();
            return result;
        }

        public Task<TaskDto> Update(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteSerializableAsync(
                token => UpdateCore(id, dto, changedBy, token),
                cancellationToken);

        private async Task<TaskDto> UpdateCore(Guid id, UpdateTaskDto dto, Guid changedBy, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công việc.");

            _taskRules.EnsureCanEdit(task);

            var changer = await GetManagerOrThrow(changedBy, "chỉnh sửa công việc", cancellationToken);
            await EnsureTaskAccess(
                id,
                changedBy,
                true,
                "Bạn không có quyền chỉnh sửa công việc này.",
                cancellationToken);

            ValidateDateRange(dto.StartDate, dto.DueDate);
            ValidatePlannedEffort(dto.PlannedEffortHours);
            if (!task.UnitId.HasValue)
                throw new BusinessException("Công việc không có phòng ban hợp lệ.");

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
            await AddHistoryIfChanged(
                task,
                changedBy,
                "PlannedEffortHours",
                FormatHours(task.PlannedEffortHours),
                FormatHours(dto.PlannedEffortHours),
                cancellationToken);
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
            task.PlannedEffortHours = dto.PlannedEffortHours;
            task.Priority = newPriority;
            task.ProjectId = project?.Id;

            _taskRepo.Update(task);
            _context.SetOriginalRowVersion(task, ConcurrencyToken.Require(dto.RowVersion));

            var assignedUserIds = await _workflowService.ResolveTaskRecipients(task.Id, cancellationToken);
            foreach (var uid in assignedUserIds.Where(uid => uid != changedBy))
                await _notificationService.AddNotification(uid, $"Công việc '{task.Title}' đã được cập nhật.", cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
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
                ?? throw new NotFoundException("Không tìm thấy công việc.");

            await GetManagerOrThrow(userId, "xóa công việc", cancellationToken);
            await EnsureTaskAccess(id, userId, true, "Bạn không có quyền xóa công việc này.", cancellationToken);
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
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task RemindTask(Guid taskId, Guid reminderId, CancellationToken cancellationToken = default)
            => RemindTaskCore(taskId, reminderId, cancellationToken);

        private async Task RemindTaskCore(Guid taskId, Guid reminderId, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công việc.");

            if (task.Status == TaskStatusEnum.Approved)
                throw new BusinessException("Công việc đã hoàn thành, không cần đôn đốc.");

            await GetManagerOrThrow(reminderId, "đôn đốc công việc", cancellationToken);
            await EnsureTaskAccess(taskId, reminderId, true, "Bạn không có quyền đôn đốc công việc này.", cancellationToken);

            var recipients = await _workflowService.ResolveTaskRecipients(taskId, cancellationToken);
            if (!recipients.Any())
                throw new BusinessException("Không có nhân viên nào được giao công việc này.");

            var reminder = await _userRepo.GetByIdAsync(reminderId, cancellationToken);
            foreach (var uid in recipients)
                await _notificationService.AddNotification(
                    uid,
                    $"{reminder?.FullName ?? "Quản lý"} đã nhắc bạn về công việc: {task.Title}",
                    cancellationToken);

            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                ChangedBy = reminderId,
                FieldName = "Remind",
                OldValue = string.Empty,
                NewValue = "Đã gửi nhắc nhở tiến độ"
            }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<User> GetManagerOrThrow(Guid userId, string action, CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người dùng.");

            if (user.Role != SystemRoles.Manager)
                throw new ForbiddenException($"Chỉ Trưởng phòng mới được {action}.");

            return user;
        }

        private async Task EnsureTaskAccess(
            Guid taskId,
            Guid userId,
            bool managementOnly,
            string message,
            CancellationToken cancellationToken)
        {
            if (!await _accessService.CanAccessTask(taskId, userId, managementOnly, cancellationToken))
                throw new ForbiddenException(message);
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
            if (string.IsNullOrWhiteSpace(priority))
                return TaskPriorityEnum.Medium;

            if (Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsed) &&
                Enum.IsDefined(parsed))
            {
                return parsed;
            }

            throw new BusinessException("Mức ưu tiên không hợp lệ.");
        }

        private static void ValidateDateRange(DateTime? startDate, DateTime? dueDate)
        {
            if (startDate.HasValue && dueDate.HasValue && dueDate.Value < startDate.Value)
                throw new BusinessException("Hạn hoàn thành không được sớm hơn ngày bắt đầu.");
        }

        private static void ValidatePlannedEffort(decimal? plannedEffortHours)
        {
            if (plannedEffortHours.HasValue &&
                plannedEffortHours.Value is <= 0m or > 100000m)
            {
                throw new BusinessException("Khối lượng kế hoạch phải lớn hơn 0 và không vượt quá 100000 giờ.");
            }
        }

        private static string? FormatHours(decimal? hours)
            => hours?.ToString(CultureInfo.InvariantCulture);

    }
}
