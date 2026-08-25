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
                ?? throw new NotFoundException("Không tìm thấy công việc.");

            if (task.IsDeleted)
                throw new NotFoundException("Không tìm thấy công việc.");

            if (task.Status == TaskStatusEnum.Approved)
                throw new BusinessException("Công việc đã hoàn thành, không thể báo cáo thêm tiến độ.");

            var reporter = await _userRepo.GetByIdAsync(reporterId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người dùng.");
            if (reporter.Role != SystemRoles.User)
                throw new ForbiddenException("Chỉ nhân viên mới được báo cáo tiến độ.");

            if (!await _accessService.CanAccessTask(
                    dto.TaskId,
                    reporterId,
                    cancellationToken: cancellationToken))
                throw new ForbiddenException("Bạn không có quyền báo cáo tiến độ cho công việc này.");

            await _workflowService.EnsureDependenciesCompletedAsync(
                dto.TaskId,
                cancellationToken);

            var hasPendingCompletion = await _repo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == dto.TaskId &&
                p.UserId == reporterId &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            if (hasPendingCompletion)
                throw new BusinessException("Bạn đang có báo cáo hoàn thành chờ duyệt, vui lòng chờ Trưởng phòng xử lý trước khi báo cáo tiếp.");

            var hasPendingSubmittedForTask = await _repo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == dto.TaskId &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            dto.Percent = Math.Clamp(dto.Percent, 0, 100);
            dto.HoursSpent = Math.Max(0, dto.HoursSpent);

            var requiresReview = task.RequiresReview || dto.SubmitForReview == true;
            if (dto.Percent == 100 && requiresReview && !dto.FileId.HasValue)
                throw new BusinessException("Vui lòng đính kèm tệp minh chứng khi nộp báo cáo hoàn thành.");

            if (dto.Percent == 100)
            {
                var hasCompleted = await _repo.QueryReadOnly().AnyAsync(p =>
                    p.TaskId == dto.TaskId &&
                    p.UserId == reporterId &&
                    p.Percent >= 100 &&
                    (p.Status == ProgressStatusEnum.Submitted || p.Status == ProgressStatusEnum.Approved), cancellationToken);

                if (hasCompleted)
                    throw new BusinessException("Bạn đã có báo cáo hoàn thành đang chờ duyệt hoặc đã được duyệt cho công việc này.");
            }

            UploadFile? file = null;
            if (dto.FileId.HasValue)
            {
                file = await _uploadRepo.GetByIdAsync(dto.FileId.Value, cancellationToken)
                    ?? throw new NotFoundException("Tệp đính kèm không tồn tại.");

                if (file.UploadedBy.HasValue && file.UploadedBy.Value != reporterId)
                    throw new ForbiddenException("Bạn không có quyền dùng tệp đính kèm này.");

                if (file.TaskId != task.Id)
                    throw new ForbiddenException("Tệp đính kèm không thuộc công việc này.");

                if (file.ProgressId.HasValue)
                    throw new BusinessException("Tệp đính kèm này đã được sử dụng cho báo cáo khác.");
            }

            var progress = _mapper.Map<Progress>(dto);
            progress.Id = Guid.NewGuid();
            progress.UserId = reporterId;
            progress.UpdatedAt = DateTime.UtcNow;
            progress.HoursSpent = dto.HoursSpent;
            progress.Status = _workflowService.ResolveNewProgressStatus(
                dto.Percent,
                requiresReview);

            await _repo.AddAsync(progress, cancellationToken);

            if (file != null)
            {
                file.ProgressId = progress.Id;
                _uploadRepo.Update(file);
            }

            await _workflowService.ApplyProgressReportAsync(
                task,
                progress,
                reporterId,
                hasPendingSubmittedForTask,
                cancellationToken);

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
                    $"Nhân viên {user.FullName} đã nộp báo cáo tiến độ cho công việc: {task.Title}",
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
