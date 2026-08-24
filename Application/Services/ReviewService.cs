using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IGenericRepository<ReportReview> _reviewRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly INotificationService _notificationService;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskWorkflowService _workflowService;
        private readonly ITransactionManager _transactionManager;
        private readonly IAppDbContext _context;

        public ReviewService(
            IGenericRepository<Progress> progressRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<TaskItem> taskRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            ITransactionManager transactionManager,
            IAppDbContext context)
        {
            _progressRepo = progressRepo;
            _reviewRepo = reviewRepo;
            _taskRepo = taskRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _transactionManager = transactionManager;
            _context = context;
        }

        public Task<ReviewDto> Review(ReviewDto dto, Guid reviewerId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteAsync(
                token => ReviewCore(dto, reviewerId, token),
                cancellationToken);

        private async Task<ReviewDto> ReviewCore(
            ReviewDto dto,
            Guid reviewerId,
            CancellationToken cancellationToken)
        {
            var progress = await _progressRepo.GetByIdAsync(dto.ProgressId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy báo cáo tiến độ.");

            var reviewerRole = await _accessService.GetUserRole(reviewerId, cancellationToken);
            if (reviewerRole != SystemRoles.Manager)
                throw new ForbiddenException("Chỉ Trưởng phòng mới được duyệt báo cáo.");

            if (!await _accessService.CanAccessTask(
                    progress.TaskId,
                    reviewerId,
                    managementOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Bạn không có quyền duyệt báo cáo này.");

            if (progress.Status != ProgressStatusEnum.Submitted)
                throw new BusinessException("Báo cáo này không ở trạng thái chờ duyệt hoặc đã được xử lý.");

            var alreadyReviewed = await _reviewRepo.QueryReadOnly()
                .AnyAsync(r => r.ProgressId == dto.ProgressId, cancellationToken);
            if (alreadyReviewed)
                throw new BusinessException("Báo cáo này đã có kết quả duyệt.");

            var task = await _taskRepo.GetByIdAsync(progress.TaskId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy công việc.");

            var hasOtherSubmittedProgress = await _progressRepo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == progress.TaskId &&
                p.Id != progress.Id &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            var normalizedComment = string.IsNullOrWhiteSpace(dto.Comment)
                ? null
                : dto.Comment.Trim();

            await _workflowService.ApplyReviewDecisionAsync(
                task,
                progress,
                dto.Approve,
                reviewerId,
                hasOtherSubmittedProgress,
                normalizedComment,
                cancellationToken);

            _progressRepo.Update(progress);
            _taskRepo.Update(task);

            await _reviewRepo.AddAsync(new ReportReview
            {
                Id = Guid.NewGuid(),
                ProgressId = dto.ProgressId,
                IsApproved = dto.Approve,
                Comment = normalizedComment,
                ReviewedAt = DateTime.UtcNow,
                ReviewerId = reviewerId
            }, cancellationToken);

            var message = dto.Approve
                ? $"Báo cáo của bạn đã được phê duyệt.{(normalizedComment == null ? "" : $" Ghi chú: {normalizedComment}")}"
                : $"Báo cáo của bạn bị từ chối. Lý do: {normalizedComment}";

            await _notificationService.AddNotification(progress.UserId, message, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return dto;
        }

    }
}
