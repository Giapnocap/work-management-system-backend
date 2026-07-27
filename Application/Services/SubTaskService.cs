using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class SubTaskService : ISubTaskService
    {
        private readonly IGenericRepository<SubTask> _repo;
        private readonly ITaskAccessService _accessService;
        private readonly IHubContext<DiscussionHub> _hubContext;
        private readonly IMapper _mapper;

        public SubTaskService(
            IGenericRepository<SubTask> repo,
            ITaskAccessService accessService,
            IHubContext<DiscussionHub> hubContext,
            IMapper mapper)
        {
            _repo = repo;
            _accessService = accessService;
            _hubContext = hubContext;
            _mapper = mapper;
        }

        public async Task<SubTaskDto> AddSubTask(
            CreateSubTaskDto dto,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(
                    dto.TaskId,
                    userId,
                    managerOrCreatorOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Ban khong co quyen them cong viec con.");

            var title = dto.Title.Trim();
            if (title.Length == 0)
                throw new BusinessException("Ten cong viec con khong duoc de trong.");

            var exists = await _repo.QueryReadOnly()
                .AnyAsync(s => s.TaskId == dto.TaskId && s.Title == title, cancellationToken);
            if (exists) throw new BusinessException("Cong viec con nay da ton tai trong task.");

            var subTask = new SubTask
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                Title = title,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(subTask, cancellationToken);
            await _repo.SaveAsync(cancellationToken);

            var result = _mapper.Map<SubTaskDto>(subTask);
            await _hubContext.Clients.Group(dto.TaskId.ToString())
                .SendAsync("ReceiveSubTaskAdded", result, cancellationToken);
            return result;
        }

        public async Task ToggleSubTask(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var subTask = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Sub-task not found");

            if (!await _accessService.CanAccessTask(subTask.TaskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen cap nhat cong viec con nay.");

            subTask.IsCompleted = !subTask.IsCompleted;
            _repo.Update(subTask);
            await _repo.SaveAsync(cancellationToken);

            await _hubContext.Clients.Group(subTask.TaskId.ToString())
                .SendAsync("ReceiveSubTaskToggled", id, subTask.IsCompleted, cancellationToken);
        }

        public async Task Delete(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var subTask = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Sub-task not found");

            if (!await _accessService.CanAccessTask(
                    subTask.TaskId,
                    userId,
                    managerOrCreatorOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Ban khong co quyen xoa cong viec con nay.");

            var taskId = subTask.TaskId;
            _repo.Delete(subTask);
            await _repo.SaveAsync(cancellationToken);

            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("ReceiveSubTaskDeleted", id, cancellationToken);
        }

        public async Task<List<SubTaskDto>> GetSubTasks(
            Guid taskId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(taskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen xem cong viec con.");

            var list = await _repo.QueryReadOnly()
                .Where(s => s.TaskId == taskId)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SubTaskDto>>(list);
        }
    }
}
