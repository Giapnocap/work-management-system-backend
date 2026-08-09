using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
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
        private readonly INotificationService _notificationService;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskWorkflowService _workflowService;
        private readonly IMapper _mapper;
        private readonly ITransactionManager _transactionManager;
        private readonly IAppDbContext _context;

        public ProgressService(
            IGenericRepository<Progress> repo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<UploadFile> uploadRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            IMapper mapper,
            ITransactionManager transactionManager,
            IAppDbContext context)
        {
            _repo = repo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _uploadRepo = uploadRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _mapper = mapper;
            _transactionManager = transactionManager;
            _context = context;
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
            if (reporter.Role != SystemRoles.User)
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

            await _context.SaveChangesAsync(cancellationToken);
            var result = _mapper.Map<ProgressDto>(progress);
            result.TaskTitle = task.Title;
            result.RequiresReview = requiresReview;
            result.Files = file == null ? new List<UploadFileDto>() : new List<UploadFileDto> { MapFile(file) };
            return result;
        }

        private async Task NotifyManagers(
            TaskItem task,
            Guid submitterId,
            CancellationToken cancellationToken)
        {
            var user = await _userRepo.GetByIdAsync(submitterId, cancellationToken);
            if (user?.UnitId == null) return;

            var managers = await _userRepo.QueryReadOnly()
                .Where(u => u.Role == SystemRoles.Manager && u.UnitId == user.UnitId && !u.IsDeleted)
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
