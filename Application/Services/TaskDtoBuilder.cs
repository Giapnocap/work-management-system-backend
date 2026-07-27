using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Application.Services
{
    public class TaskDtoBuilder : ITaskDtoBuilder
    {
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Unit> _unitRepo;
        private readonly IGenericRepository<UploadFile> _uploadRepo;
        private readonly IGenericRepository<SubTask> _subTaskRepo;
        private readonly IMapper _mapper;

        public TaskDtoBuilder(
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<Unit> unitRepo,
            IGenericRepository<UploadFile> uploadRepo,
            IGenericRepository<SubTask> subTaskRepo,
            IMapper mapper)
        {
            _assigneeRepo = assigneeRepo;
            _userRepo = userRepo;
            _unitRepo = unitRepo;
            _uploadRepo = uploadRepo;
            _subTaskRepo = subTaskRepo;
            _mapper = mapper;
        }

        public async Task<TaskDto> BuildTaskDto(
            TaskItem task,
            CancellationToken cancellationToken = default)
        {
            var list = await BuildTaskDtos(new[] { task }, cancellationToken);
            return list.Single();
        }

        public async Task<List<TaskDto>> BuildTaskDtos(
            IReadOnlyCollection<TaskItem> tasks,
            CancellationToken cancellationToken = default)
        {
            if (tasks.Count == 0)
                return new List<TaskDto>();

            var taskIds = tasks.Select(t => t.Id).ToList();
            var unitIds = tasks.Where(t => t.UnitId.HasValue).Select(t => t.UnitId!.Value).Distinct().ToList();
            var creatorIds = tasks.Select(t => t.CreatedBy).Distinct().ToList();

            var unitMap = await _unitRepo.QueryReadOnly()
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

            var creatorMap = await _userRepo.QueryReadOnly()
                .Where(u => creatorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

            var assignees = await _assigneeRepo.QueryReadOnly()
                .Where(a => taskIds.Contains(a.TaskId) && a.UserId.HasValue)
                .Join(_userRepo.QueryReadOnly(),
                    a => a.UserId,
                    u => u.Id,
                    (a, u) => new { a.TaskId, u.Id, u.FullName, u.EmployeeCode })
                .ToListAsync(cancellationToken);

            var files = await _uploadRepo.QueryReadOnly()
                .Where(f => taskIds.Contains(f.TaskId))
                .ToListAsync(cancellationToken);

            var subTasks = await _subTaskRepo.QueryReadOnly()
                .Where(s => taskIds.Contains(s.TaskId))
                .ToListAsync(cancellationToken);

            var assigneesByTask = assignees.ToLookup(assignee => assignee.TaskId);
            var filesByTask = files.ToLookup(file => file.TaskId);
            var subTasksByTask = subTasks.ToLookup(subTask => subTask.TaskId);

            return tasks.Select(task =>
            {
                var dto = _mapper.Map<TaskDto>(task);
                dto.Priority = task.Priority.ToString();
                dto.UnitName = task.UnitId.HasValue && unitMap.TryGetValue(task.UnitId.Value, out var unitName) ? unitName : null;
                dto.CreatedByName = creatorMap.TryGetValue(task.CreatedBy, out var creatorName) ? creatorName : null;
                dto.Assignees = assigneesByTask[task.Id]
                    .Select(a => new TaskAssigneeDto
                    {
                        Id = a.Id,
                        FullName = a.FullName ?? "-",
                        EmployeeCode = a.EmployeeCode ?? "-"
                    })
                    .ToList();
                dto.Files = filesByTask[task.Id].Select(MapFile).ToList();
                dto.SubTasks = _mapper.Map<List<SubTaskDto>>(subTasksByTask[task.Id]);
                return dto;
            }).ToList();
        }

        private static UploadFileDto MapFile(UploadFile file)
        {
            return new UploadFileDto
            {
                Id = file.Id,
                FileName = file.FileName,
                CreatedAt = file.CreatedAt,
                ProgressId = file.ProgressId,
                TaskId = file.TaskId,
                UploadedBy = file.UploadedBy
            };
        }
    }
}
