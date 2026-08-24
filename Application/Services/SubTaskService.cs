using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class SubTaskService : ISubTaskService
    {
        private readonly IGenericRepository<SubTask> _repo;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskRealtimeNotifier _realtimeNotifier;
        private readonly IMapper _mapper;
        private readonly IAppDbContext _context;

        public SubTaskService(
            IGenericRepository<SubTask> repo,
            ITaskAccessService accessService,
            ITaskRealtimeNotifier realtimeNotifier,
            IMapper mapper,
            IAppDbContext context)
        {
            _repo = repo;
            _accessService = accessService;
            _realtimeNotifier = realtimeNotifier;
            _mapper = mapper;
            _context = context;
        }

        public async Task<SubTaskDto> AddSubTask(
            CreateSubTaskDto dto,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(
                    dto.TaskId,
                    userId,
                    managementOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Bạn không có quyền thêm công việc con.");

            var title = dto.Title.Trim();
            if (title.Length == 0)
                throw new BusinessException("Tên công việc con không được để trống.");

            var exists = await _repo.QueryReadOnly()
                .AnyAsync(s => s.TaskId == dto.TaskId && s.Title == title, cancellationToken);
            if (exists) throw new BusinessException("Công việc con này đã tồn tại trong công việc.");

            var subTask = new SubTask
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                Title = title,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(subTask, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var result = _mapper.Map<SubTaskDto>(subTask);
            await _realtimeNotifier.SubTaskAddedAsync(
                dto.TaskId,
                result,
                cancellationToken);
            return result;
        }

        public async Task ToggleSubTask(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var subTask = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công việc con.");

            if (!await _accessService.CanAccessTask(subTask.TaskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Bạn không có quyền cập nhật công việc con này.");

            subTask.IsCompleted = !subTask.IsCompleted;
            _repo.Update(subTask);
            await _context.SaveChangesAsync(cancellationToken);

            await _realtimeNotifier.SubTaskToggledAsync(
                subTask.TaskId,
                id,
                subTask.IsCompleted,
                cancellationToken);
        }

        public async Task Delete(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var subTask = await _repo.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công việc con.");

            if (!await _accessService.CanAccessTask(
                    subTask.TaskId,
                    userId,
                    managementOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Bạn không có quyền xóa công việc con này.");

            var taskId = subTask.TaskId;
            _repo.Delete(subTask);
            await _context.SaveChangesAsync(cancellationToken);

            await _realtimeNotifier.SubTaskDeletedAsync(taskId, id, cancellationToken);
        }

        public async Task<List<SubTaskDto>> GetSubTasks(
            Guid taskId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(taskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Bạn không có quyền xem công việc con.");

            var list = await _repo.QueryReadOnly()
                .Where(s => s.TaskId == taskId)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SubTaskDto>>(list);
        }
    }
}
