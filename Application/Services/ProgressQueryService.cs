using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Application.Services
{
    public sealed class ProgressQueryService : IProgressQueryService
    {
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<UploadFile> _uploadRepo;
        private readonly IGenericRepository<ReportReview> _reviewRepo;
        private readonly IGenericRepository<Unit> _unitRepo;
        private readonly ITaskAccessService _accessService;
        private readonly IMapper _mapper;

        public ProgressQueryService(
            IGenericRepository<Progress> progressRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<UploadFile> uploadRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<Unit> unitRepo,
            ITaskAccessService accessService,
            IMapper mapper)
        {
            _progressRepo = progressRepo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _uploadRepo = uploadRepo;
            _reviewRepo = reviewRepo;
            _unitRepo = unitRepo;
            _accessService = accessService;
            _mapper = mapper;
        }

        public async Task<PagedResult<ProgressDto>> GetAll(
            int page,
            int size,
            Guid requesterId,
            bool myProgress = false,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size);
            page = paging.Page;
            size = paging.Size;

            var requester = await _userRepo.QueryReadOnly()
                .Where(user => user.Id == requesterId && user.IsApproved && !user.IsDeleted)
                .Select(user => new { user.Role, user.UnitId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var query = _progressRepo.QueryReadOnly();

            if (myProgress || requester.Role == SystemRoles.User)
            {
                query = query.Where(progress => progress.UserId == requesterId);
            }
            else if (requester.Role == SystemRoles.Manager)
            {
                query = requester.UnitId.HasValue
                    ? query.Where(progress => _taskRepo.QueryReadOnly().Any(task =>
                        task.Id == progress.TaskId &&
                        task.UnitId == requester.UnitId.Value &&
                        !task.IsDeleted))
                    : query.Where(_ => false);
            }
            else if (requester.Role != SystemRoles.Admin)
            {
                query = query.Where(progress => progress.UserId == requesterId);
            }

            var total = await query.CountAsync(cancellationToken);
            var progresses = await query
                .OrderByDescending(progress => progress.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await BuildProgressDtos(progresses, cancellationToken: cancellationToken);
            return new PagedResult<ProgressDto>(total, page, size, dtos);
        }

        public async Task<List<ProgressDto>> GetByTaskAsync(
            Guid taskId,
            Guid requesterId,
            CancellationToken cancellationToken = default)
        {
            _ = await _taskRepo.GetByIdAsync(taskId, cancellationToken)
                ?? throw new NotFoundException("Task not found.");

            if (!await _accessService.CanAccessTask(
                    taskId,
                    requesterId,
                    cancellationToken: cancellationToken))
            {
                throw new ForbiddenException("Ban khong co quyen xem lich su bao cao cua cong viec nay.");
            }

            var progresses = await _progressRepo.QueryReadOnly()
                .Where(progress => progress.TaskId == taskId)
                .OrderByDescending(progress => progress.UpdatedAt)
                .ToListAsync(cancellationToken);

            return await BuildProgressDtos(progresses, cancellationToken: cancellationToken);
        }

        public async Task<PagedResult<ProgressDto>> GetMyHistory(
            Guid requesterId,
            int page,
            int size,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size, Paging.DefaultHistoryPageSize);
            page = paging.Page;
            size = paging.Size;

            var requester = await _userRepo.QueryReadOnly()
                .Where(user => user.Id == requesterId && user.IsApproved && !user.IsDeleted)
                .Select(user => new { user.Role, user.UnitId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var query = _progressRepo.QueryReadOnly();
            if (requester.Role == SystemRoles.Manager)
            {
                query = requester.UnitId.HasValue
                    ? query.Where(progress => _taskRepo.QueryReadOnly()
                        .Any(task => task.Id == progress.TaskId && task.UnitId == requester.UnitId.Value))
                    : query.Where(_ => false);
            }
            else if (requester.Role != SystemRoles.Admin)
            {
                query = query.Where(progress => progress.UserId == requesterId);
            }

            var total = await query.CountAsync(cancellationToken);
            var progresses = await query
                .OrderByDescending(progress => progress.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await BuildProgressDtos(
                progresses,
                includeUnitName: true,
                cancellationToken: cancellationToken);
            return new PagedResult<ProgressDto>(total, page, size, dtos);
        }

        private async Task<List<ProgressDto>> BuildProgressDtos(
            List<Progress> progresses,
            bool includeUnitName = false,
            CancellationToken cancellationToken = default)
        {
            var userIds = progresses.Select(progress => progress.UserId).Distinct().ToList();
            var taskIds = progresses.Select(progress => progress.TaskId).Distinct().ToList();
            var progressIds = progresses.Select(progress => progress.Id).ToList();

            var users = await _userRepo.QueryReadOnly()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user, cancellationToken);

            var tasks = await _taskRepo.QueryReadOnly()
                .Where(task => taskIds.Contains(task.Id))
                .ToDictionaryAsync(task => task.Id, task => task, cancellationToken);

            var unitIds = tasks.Values
                .Where(task => task.UnitId.HasValue)
                .Select(task => task.UnitId!.Value)
                .Distinct()
                .ToList();
            var units = includeUnitName
                ? await _unitRepo.QueryReadOnly()
                    .Where(unit => unitIds.Contains(unit.Id))
                    .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken)
                : new Dictionary<Guid, string>();

            var files = await _uploadRepo.QueryReadOnly()
                .Where(file => file.ProgressId.HasValue && progressIds.Contains(file.ProgressId.Value))
                .ToListAsync(cancellationToken);

            var reviews = await _reviewRepo.QueryReadOnly()
                .Where(review => progressIds.Contains(review.ProgressId))
                .ToDictionaryAsync(review => review.ProgressId, review => review, cancellationToken);

            return progresses.Select(progress =>
            {
                var dto = _mapper.Map<ProgressDto>(progress);
                users.TryGetValue(progress.UserId, out var user);
                tasks.TryGetValue(progress.TaskId, out var task);

                dto.UserFullName = user?.FullName ?? "-";
                dto.UserEmployeeCode = user?.EmployeeCode ?? "-";
                dto.TaskTitle = task?.Title ?? "-";
                dto.RequiresReview = task?.RequiresReview ?? false;
                dto.ReviewComment = reviews.TryGetValue(progress.Id, out var review) ? review.Comment : null;
                dto.UnitName = includeUnitName &&
                               task?.UnitId != null &&
                               units.TryGetValue(task.UnitId.Value, out var unitName)
                    ? unitName
                    : dto.UnitName;
                dto.Files = files
                    .Where(file => file.ProgressId == progress.Id)
                    .Select(MapFile)
                    .ToList();
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
