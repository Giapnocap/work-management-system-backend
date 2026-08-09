using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public sealed class TaskQueryService : ITaskQueryService
    {
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskHistory> _historyRepo;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskDtoBuilder _taskDtoBuilder;

        public TaskQueryService(
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskHistory> historyRepo,
            ITaskAccessService accessService,
            ITaskDtoBuilder taskDtoBuilder)
        {
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _userRepo = userRepo;
            _historyRepo = historyRepo;
            _accessService = accessService;
            _taskDtoBuilder = taskDtoBuilder;
        }

        public async Task<PagedResult<TaskDto>> Get(
            string keyword,
            int page,
            int size,
            string? status,
            Guid requesterId,
            bool myTasks = false,
            Guid? projectId = null,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size);
            page = paging.Page;
            size = paging.Size;

            var requester = await _userRepo.GetByIdAsync(requesterId, cancellationToken)
                ?? throw new NotFoundException("User not found.");
            var assigneeId = myTasks ? requesterId : (Guid?)null;

            var query = _taskRepo.QueryReadOnly().Where(task => !task.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(task => task.Title.Contains(keyword.Trim()));

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<TaskStatusEnum>(status, true, out var statusEnum))
            {
                query = query.Where(task => task.Status == statusEnum);
            }

            if (projectId.HasValue)
                query = query.Where(task => task.ProjectId == projectId.Value);

            if (requester.Role == SystemRoles.Admin)
            {
                if (assigneeId.HasValue)
                {
                    query = query.Where(task => _assigneeRepo.QueryReadOnly()
                        .Any(assignee => assignee.TaskId == task.Id && assignee.UserId == assigneeId.Value));
                }
            }
            else if (requester.Role == SystemRoles.Manager)
            {
                if (!requester.UnitId.HasValue)
                {
                    query = query.Where(_ => false);
                }
                else
                {
                    var managerUnitId = requester.UnitId.Value;
                    query = query.Where(task =>
                        task.UnitId == managerUnitId ||
                        _assigneeRepo.QueryReadOnly().Any(assignee =>
                            assignee.TaskId == task.Id && assignee.UnitId == managerUnitId));

                    if (assigneeId.HasValue)
                    {
                        query = query.Where(task => _assigneeRepo.QueryReadOnly()
                            .Any(assignee => assignee.TaskId == task.Id && assignee.UserId == assigneeId.Value));
                    }
                }
            }
            else
            {
                var accessibleTaskIds = await GetAccessibleTaskIdsForUser(requester, cancellationToken);
                query = query.Where(task => accessibleTaskIds.Contains(task.Id));
            }

            var total = await query.CountAsync(cancellationToken);
            var tasks = await query
                .OrderBy(task => task.DueDate == null)
                .ThenBy(task => task.DueDate)
                .ThenByDescending(task => task.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await _taskDtoBuilder.BuildTaskDtos(tasks, cancellationToken);
            return new PagedResult<TaskDto>(total, page, size, dtos);
        }

        public async Task<TaskDto> GetByIdAsync(
            Guid id,
            Guid requesterId,
            CancellationToken cancellationToken = default)
        {
            var task = await _taskRepo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            await EnsureTaskAccess(
                id,
                requesterId,
                "Ban khong co quyen xem cong viec nay.",
                cancellationToken);

            return await _taskDtoBuilder.BuildTaskDto(task, cancellationToken);
        }

        public async Task<List<TaskHistoryDto>> GetHistoryAsync(
            Guid taskId,
            Guid requesterId,
            CancellationToken cancellationToken = default)
        {
            await EnsureTaskAccess(
                taskId,
                requesterId,
                "Ban khong co quyen xem lich su cong viec nay.",
                cancellationToken);

            return await _historyRepo.QueryReadOnly()
                .Where(history => history.TaskId == taskId)
                .OrderBy(history => history.ChangedAt)
                .Select(history => new TaskHistoryDto
                {
                    Id = history.Id,
                    TaskId = history.TaskId,
                    ChangedBy = history.ChangedBy,
                    FieldName = history.FieldName,
                    OldValue = history.OldValue,
                    NewValue = history.NewValue,
                    ChangedAt = history.ChangedAt
                })
                .ToListAsync(cancellationToken);
        }

        private async Task EnsureTaskAccess(
            Guid taskId,
            Guid requesterId,
            string message,
            CancellationToken cancellationToken)
        {
            if (!await _accessService.CanAccessTask(
                    taskId,
                    requesterId,
                    cancellationToken: cancellationToken))
            {
                throw new ForbiddenException(message);
            }
        }

        private async Task<List<Guid>> GetAccessibleTaskIdsForUser(
            User user,
            CancellationToken cancellationToken)
        {
            var directTaskIds = await _assigneeRepo.QueryReadOnly()
                .Where(assignee => assignee.UserId == user.Id)
                .Select(assignee => assignee.TaskId)
                .ToListAsync(cancellationToken);

            if (!user.UnitId.HasValue)
                return directTaskIds.Distinct().ToList();

            var unitId = user.UnitId.Value;
            var unitTaskIds = await _assigneeRepo.QueryReadOnly()
                .Where(assignee =>
                    assignee.UnitId == unitId &&
                    !_assigneeRepo.QueryReadOnly().Any(direct =>
                        direct.TaskId == assignee.TaskId && direct.UserId.HasValue))
                .Select(assignee => assignee.TaskId)
                .ToListAsync(cancellationToken);

            return directTaskIds.Union(unitTaskIds).Distinct().ToList();
        }
    }
}
