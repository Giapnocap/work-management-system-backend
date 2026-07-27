using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ProgressService : IProgressService
    {
        private readonly IGenericRepository<Progress> _repo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<UploadFile> _uploadRepo;
        private readonly IGenericRepository<ReportReview> _reviewRepo;
        private readonly IGenericRepository<Unit> _unitRepo;
        private readonly INotificationService _notificationService;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskWorkflowService _workflowService;
        private readonly IMapper _mapper;
        private readonly ITransactionManager _transactionManager;

        public ProgressService(
            IGenericRepository<Progress> repo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<UploadFile> uploadRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<Unit> unitRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            IMapper mapper,
            ITransactionManager transactionManager)
        {
            _repo = repo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _uploadRepo = uploadRepo;
            _reviewRepo = reviewRepo;
            _unitRepo = unitRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _mapper = mapper;
            _transactionManager = transactionManager;
        }

        public Task<ProgressDto> Update(CreateProgressDto dto, Guid reporterId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteAsync(token => UpdateCore(dto, reporterId, token), cancellationToken);

        private async Task<ProgressDto> UpdateCore(CreateProgressDto dto, Guid reporterId, CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(dto.TaskId, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            if (task.IsDeleted)
                throw new NotFoundException("Task not found");

            if (task.Status == TaskStatusEnum.Approved)
                throw new BusinessException("Cong viec da hoan thanh, khong the bao cao them tien do.");

            var reporter = await _userRepo.GetByIdAsync(reporterId, cancellationToken)
                ?? throw new NotFoundException("User not found.");
            if (reporter.Role != "User")
                throw new ForbiddenException("Chi nhan vien moi duoc bao cao tien do.");

            if (!await _accessService.CanAccessTask(
                    dto.TaskId,
                    reporterId,
                    cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen bao cao tien do cho cong viec nay.");

            var hasPendingCompletion = await _repo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == dto.TaskId &&
                p.UserId == reporterId &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            if (hasPendingCompletion)
                throw new BusinessException("Ban dang co bao cao hoan thanh cho duyet, vui long cho quan ly xu ly truoc khi bao cao tiep.");

            var hasPendingSubmittedForTask = await _repo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == dto.TaskId &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            dto.Percent = Math.Clamp(dto.Percent, 0, 100);
            dto.HoursSpent = Math.Max(0, dto.HoursSpent);

            var requiresReview = task.RequiresReview || dto.SubmitForReview == true;
            if (dto.Percent == 100 && requiresReview && !dto.FileId.HasValue)
                throw new BusinessException("Vui long dinh kem file minh chung khi nop bao cao hoan thanh.");

            if (dto.Percent == 100)
            {
                var hasCompleted = await _repo.QueryReadOnly().AnyAsync(p =>
                    p.TaskId == dto.TaskId &&
                    p.UserId == reporterId &&
                    p.Percent >= 100 &&
                    (p.Status == ProgressStatusEnum.Submitted || p.Status == ProgressStatusEnum.Approved), cancellationToken);

                if (hasCompleted)
                    throw new BusinessException("Ban da co bao cao hoan thanh dang cho duyet hoac da duoc duyet cho cong viec nay.");
            }

            UploadFile? file = null;
            if (dto.FileId.HasValue)
            {
                file = await _uploadRepo.GetByIdAsync(dto.FileId.Value, cancellationToken)
                    ?? throw new NotFoundException("File dinh kem khong ton tai.");

                if (file.UploadedBy.HasValue && file.UploadedBy.Value != reporterId)
                    throw new ForbiddenException("Ban khong co quyen dung file dinh kem nay.");

                if (file.TaskId != task.Id)
                    throw new ForbiddenException("File dinh kem khong thuoc cong viec nay.");

                if (file.ProgressId.HasValue)
                    throw new BusinessException("File dinh kem nay da duoc su dung cho bao cao khac.");
            }

            var progress = _mapper.Map<Progress>(dto);
            progress.Id = Guid.NewGuid();
            progress.UserId = reporterId;
            progress.UpdatedAt = DateTime.UtcNow;
            progress.HoursSpent = dto.HoursSpent;
            progress.Status = dto.Percent == 100
                ? (requiresReview ? ProgressStatusEnum.Submitted : ProgressStatusEnum.Approved)
                : ProgressStatusEnum.InProgress;

            await _repo.AddAsync(progress, cancellationToken);

            if (file != null)
            {
                file.ProgressId = progress.Id;
                _uploadRepo.Update(file);
            }

            if (progress.Status == ProgressStatusEnum.Approved)
            {
                task.ActualHours += progress.HoursSpent;
                await _workflowService.ApplyCompletionStateAsync(task, progress.UserId, cancellationToken);
            }
            else if (progress.Status == ProgressStatusEnum.Submitted)
            {
                task.Status = TaskStatusEnum.Submitted;
            }
            else if (task.Status != TaskStatusEnum.Approved)
            {
                task.Status = hasPendingSubmittedForTask
                    ? TaskStatusEnum.Submitted
                    : TaskStatusEnum.InProgress;
            }

            _taskRepo.Update(task);

            if (progress.Status == ProgressStatusEnum.Submitted)
                await NotifyManagers(task, reporterId, cancellationToken);

            await _repo.SaveAsync(cancellationToken);
            var result = _mapper.Map<ProgressDto>(progress);
            result.TaskTitle = task.Title;
            result.RequiresReview = requiresReview;
            result.Files = file == null ? new List<UploadFileDto>() : new List<UploadFileDto> { MapFile(file) };
            return result;
        }

        public async Task<object> GetAll(
            int page,
            int size,
            Guid? userId = null,
            Guid? unitId = null,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size);
            page = paging.Page;
            size = paging.Size;

            var query = _repo.QueryReadOnly();

            if (userId.HasValue)
                query = query.Where(p => p.UserId == userId.Value);

            if (unitId.HasValue)
            {
                var taskIdsInUnit = await _taskRepo.QueryReadOnly()
                    .Where(t => t.UnitId == unitId.Value && !t.IsDeleted)
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);

                query = query.Where(p => taskIdsInUnit.Contains(p.TaskId));
            }

            var total = await query.CountAsync(cancellationToken);
            var progresses = await query
                .OrderByDescending(p => p.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await BuildProgressDtos(progresses, cancellationToken: cancellationToken);
            return new { total, page, size, data = dtos };
        }

        public async Task<List<ProgressDto>> GetByTaskAsync(
            Guid taskId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, cancellationToken);
            if (task == null) return new List<ProgressDto>();

            if (!await _accessService.CanAccessTask(
                    taskId,
                    userId,
                    cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen xem lich su bao cao cua cong viec nay.");

            var progresses = await _repo.QueryReadOnly()
                .Where(p => p.TaskId == taskId)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync(cancellationToken);

            return await BuildProgressDtos(progresses, cancellationToken: cancellationToken);
        }

        public async Task<object> GetMyHistory(
            Guid userId,
            int page,
            int size,
            CancellationToken cancellationToken = default)
        {
            var paging = Paging.Normalize(page, size, Paging.DefaultHistoryPageSize);
            page = paging.Page;
            size = paging.Size;

            var requester = await _userRepo.QueryReadOnly()
                .Where(u => u.Id == userId && u.IsApproved && !u.IsDeleted)
                .Select(u => new { u.Role, u.UnitId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var query = _repo.QueryReadOnly();
            if (requester.Role == "Manager")
            {
                query = requester.UnitId.HasValue
                    ? query.Where(p => _taskRepo.QueryReadOnly()
                        .Any(t => t.Id == p.TaskId && t.UnitId == requester.UnitId.Value))
                    : query.Where(_ => false);
            }
            else if (requester.Role != "Admin")
            {
                query = query.Where(p => p.UserId == userId);
            }

            var total = await query.CountAsync(cancellationToken);
            var progresses = await query
                .OrderByDescending(p => p.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var dtos = await BuildProgressDtos(progresses, includeUnitName: true, cancellationToken: cancellationToken);
            return new { total, page, size, data = dtos };
        }

        private async Task<List<ProgressDto>> BuildProgressDtos(
            List<Progress> progresses,
            bool includeUnitName = false,
            CancellationToken cancellationToken = default)
        {
            var userIds = progresses.Select(p => p.UserId).Distinct().ToList();
            var taskIds = progresses.Select(p => p.TaskId).Distinct().ToList();
            var progressIds = progresses.Select(p => p.Id).ToList();

            var users = await _userRepo.QueryReadOnly()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

            var tasks = await _taskRepo.QueryReadOnly()
                .Where(t => taskIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t, cancellationToken);

            var unitIds = tasks.Values.Where(t => t.UnitId.HasValue).Select(t => t.UnitId!.Value).Distinct().ToList();
            var units = includeUnitName
                ? await _unitRepo.QueryReadOnly().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken)
                : new Dictionary<Guid, string>();

            var files = await _uploadRepo.QueryReadOnly()
                .Where(f => f.ProgressId.HasValue && progressIds.Contains(f.ProgressId.Value))
                .ToListAsync(cancellationToken);

            var reviews = await _reviewRepo.QueryReadOnly()
                .Where(r => progressIds.Contains(r.ProgressId))
                .ToDictionaryAsync(r => r.ProgressId, r => r, cancellationToken);

            return progresses.Select(p =>
            {
                var dto = _mapper.Map<ProgressDto>(p);
                users.TryGetValue(p.UserId, out var user);
                tasks.TryGetValue(p.TaskId, out var task);

                dto.UserFullName = user?.FullName ?? "-";
                dto.UserEmployeeCode = user?.EmployeeCode ?? "-";
                dto.TaskTitle = task?.Title ?? "-";
                dto.RequiresReview = task?.RequiresReview ?? false;
                dto.ReviewComment = reviews.TryGetValue(p.Id, out var review) ? review.Comment : null;
                dto.UnitName = includeUnitName && task?.UnitId != null && units.TryGetValue(task.UnitId.Value, out var unitName)
                    ? unitName
                    : dto.UnitName;
                dto.Files = files.Where(f => f.ProgressId == p.Id).Select(MapFile).ToList();
                return dto;
            }).ToList();
        }

        private async Task NotifyManagers(
            TaskItem task,
            Guid submitterId,
            CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetByIdAsync(submitterId, cancellationToken);
            if (user?.UnitId == null) return;

            var managers = await _userRepo.QueryReadOnly()
                .Where(u => u.Role == "Manager" && u.UnitId == user.UnitId && !u.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var manager in managers)
            {
                await _notificationService.AddNotification(
                    manager.Id,
                    $"Nhan vien {user.FullName} da nop bao cao tien do cho cong viec: {task.Title}",
                    cancellationToken);
            }
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
