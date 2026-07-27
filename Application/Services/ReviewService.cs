using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

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

        public ReviewService(
            IGenericRepository<Progress> progressRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<TaskItem> taskRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            ITransactionManager transactionManager)
        {
            _progressRepo = progressRepo;
            _reviewRepo = reviewRepo;
            _taskRepo = taskRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _transactionManager = transactionManager;
        }

        public Task<ReviewDto> Review(ReviewDto dto, Guid reviewerId, CancellationToken cancellationToken = default)
            => _transactionManager.ExecuteAsync(token => ReviewCore(dto, reviewerId, token), cancellationToken);

        private async Task<ReviewDto> ReviewCore(
            ReviewDto dto,
            Guid reviewerId,
            CancellationToken cancellationToken)
        {
            var progress = await _progressRepo.GetByIdAsync(dto.ProgressId, cancellationToken)
                ?? throw new NotFoundException("Progress not found");

            var reviewerRole = await _accessService.GetUserRole(reviewerId, cancellationToken);
            if (reviewerRole != "Manager")
                throw new ForbiddenException("Chi Manager moi duoc duyet bao cao.");

            if (!await _accessService.CanAccessTask(
                    progress.TaskId,
                    reviewerId,
                    managerOrCreatorOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Ban khong co quyen duyet bao cao nay.");

            if (progress.Status != ProgressStatusEnum.Submitted)
                throw new BusinessException("Bao cao nay khong o trang thai cho duyet hoac da duoc xu ly.");

            var alreadyReviewed = await _reviewRepo.QueryReadOnly()
                .AnyAsync(r => r.ProgressId == dto.ProgressId, cancellationToken);
            if (alreadyReviewed)
                throw new BusinessException("Bao cao nay da co ket qua duyet.");

            var task = await _taskRepo.GetByIdAsync(progress.TaskId, cancellationToken)
                ?? throw new NotFoundException("Task not found");

            var hasOtherSubmittedProgress = await _progressRepo.QueryReadOnly().AnyAsync(p =>
                p.TaskId == progress.TaskId &&
                p.Id != progress.Id &&
                p.Status == ProgressStatusEnum.Submitted, cancellationToken);

            progress.Status = dto.Approve ? ProgressStatusEnum.Approved : ProgressStatusEnum.Rejected;
            _progressRepo.Update(progress);

            await _reviewRepo.AddAsync(new ReportReview
            {
                Id = Guid.NewGuid(),
                ProgressId = dto.ProgressId,
                IsApproved = dto.Approve,
                Comment = dto.Comment,
                ReviewedAt = DateTime.UtcNow,
                ReviewerId = reviewerId
            }, cancellationToken);

            if (dto.Approve)
            {
                task.ActualHours += Math.Max(0, progress.HoursSpent);
                await _workflowService.ApplyCompletionStateAsync(task, progress.UserId, cancellationToken);

                if (task.Status != TaskStatusEnum.Approved && hasOtherSubmittedProgress)
                    task.Status = TaskStatusEnum.Submitted;
            }
            else if (task.Status != TaskStatusEnum.Approved)
            {
                task.Status = hasOtherSubmittedProgress
                    ? TaskStatusEnum.Submitted
                    : TaskStatusEnum.InProgress;
            }

            _taskRepo.Update(task);

            var message = dto.Approve
                ? $"Bao cao cua ban da duoc phe duyet.{(string.IsNullOrWhiteSpace(dto.Comment) ? "" : $" Ghi chu: {dto.Comment}")}"
                : $"Bao cao cua ban bi tu choi.{(string.IsNullOrWhiteSpace(dto.Comment) ? "" : $" Ly do: {dto.Comment}")}";

            await _notificationService.AddNotification(progress.UserId, message, cancellationToken);
            await _reviewRepo.SaveAsync(cancellationToken);
            return dto;
        }

    }
}
