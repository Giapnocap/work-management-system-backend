using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Services
{
    public class CommentService : ICommentService
    {
        private readonly IGenericRepository<TaskComment> _repo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<CommentReaction> _reactionRepo;
        private readonly IGenericRepository<CommentSeen> _seenRepo;
        private readonly INotificationService _notificationService;
        private readonly ITaskAccessService _accessService;
        private readonly ITaskWorkflowService _workflowService;
        private readonly ITaskRealtimeNotifier _realtimeNotifier;
        private readonly IMapper _mapper;
        private readonly IAppDbContext _context;

        public CommentService(
            IGenericRepository<TaskComment> repo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<CommentReaction> reactionRepo,
            IGenericRepository<CommentSeen> seenRepo,
            INotificationService notificationService,
            ITaskAccessService accessService,
            ITaskWorkflowService workflowService,
            ITaskRealtimeNotifier realtimeNotifier,
            IMapper mapper,
            IAppDbContext context)
        {
            _repo = repo;
            _userRepo = userRepo;
            _taskRepo = taskRepo;
            _reactionRepo = reactionRepo;
            _seenRepo = seenRepo;
            _notificationService = notificationService;
            _accessService = accessService;
            _workflowService = workflowService;
            _realtimeNotifier = realtimeNotifier;
            _mapper = mapper;
            _context = context;
        }

        public async Task<CommentDto> AddComment(
            CreateCommentDto dto,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(dto.TaskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen binh luan trong cong viec nay.");

            var content = dto.Content.Trim();
            if (content.Length == 0)
                throw new BusinessException("Noi dung binh luan khong duoc de trong.");

            var comment = new TaskComment
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(comment, cancellationToken);
            await _seenRepo.AddAsync(new CommentSeen
            {
                Id = Guid.NewGuid(),
                CommentId = comment.Id,
                UserId = userId,
                SeenAt = DateTime.UtcNow
            }, cancellationToken);

            await NotifyCommentRecipients(dto.TaskId, userId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var sender = await _userRepo.GetByIdAsync(userId, cancellationToken);
            var result = _mapper.Map<CommentDto>(comment);
            result.UserFullName = sender?.FullName;
            result.UserEmployeeCode = sender?.EmployeeCode;
            await _realtimeNotifier.CommentAddedAsync(
                dto.TaskId,
                result,
                cancellationToken);
            return result;
        }

        public async Task<List<CommentDto>> GetComments(
            Guid taskId,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            if (!userId.HasValue || !await _accessService.CanAccessTask(taskId, userId.Value, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen xem binh luan cua cong viec nay.");

            var comments = await _repo.QueryReadOnly()
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            var commentIds = comments.Select(c => c.Id).ToList();
            var allReactions = await _reactionRepo.QueryReadOnly()
                .Where(r => commentIds.Contains(r.CommentId))
                .ToListAsync(cancellationToken);
            var allSeens = await _seenRepo.QueryReadOnly()
                .Where(s => commentIds.Contains(s.CommentId))
                .ToListAsync(cancellationToken);

            var userIds = comments.Select(c => c.UserId)
                .Concat(allReactions.Select(r => r.UserId))
                .Concat(allSeens.Select(s => s.UserId))
                .Distinct()
                .ToList();

            var users = await _userRepo.QueryReadOnly()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

            return comments.Select(c =>
            {
                var dto = _mapper.Map<CommentDto>(c);
                users.TryGetValue(c.UserId, out var author);
                dto.UserFullName = author?.FullName;
                dto.UserEmployeeCode = author?.EmployeeCode;

                var reactions = allReactions.Where(r => r.CommentId == c.Id).ToList();
                dto.MyReaction = reactions.FirstOrDefault(r => r.UserId == userId.Value)?.Emoji;
                dto.Reactions = reactions
                    .GroupBy(r => r.Emoji)
                    .Select(g => new ReactionSummaryDto
                    {
                        Emoji = g.Key,
                        Count = g.Count(),
                        UserFullNames = g.Select(x => users.TryGetValue(x.UserId, out var u) ? u.FullName : "Unknown")
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct()
                            .ToList()
                    })
                    .ToList();

                dto.SeenByUserFullNames = allSeens
                    .Where(s => s.CommentId == c.Id)
                    .Select(s => users.TryGetValue(s.UserId, out var u) ? u.FullName : "Unknown")
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList();

                return dto;
            }).ToList();
        }

        public async Task MarkAsSeen(
            Guid taskId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (!await _accessService.CanAccessTask(taskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen danh dau da xem.");

            var unseenCommentIds = await _repo.QueryReadOnly()
                .Where(comment =>
                    comment.TaskId == taskId &&
                    !comment.IsDeleted &&
                    !_seenRepo.QueryReadOnly().Any(seen =>
                        seen.UserId == userId && seen.CommentId == comment.Id))
                .Select(comment => comment.Id)
                .ToListAsync(cancellationToken);

            foreach (var commentId in unseenCommentIds)
            {
                await _seenRepo.AddAsync(new CommentSeen
                {
                    Id = Guid.NewGuid(),
                    CommentId = commentId,
                    UserId = userId,
                    SeenAt = DateTime.UtcNow
                }, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _realtimeNotifier.CommentsSeenAsync(taskId, userId, cancellationToken);
        }

        public async Task ToggleReaction(
            Guid commentId,
            Guid userId,
            string emoji,
            CancellationToken cancellationToken = default)
        {
            var comment = await _repo.GetByIdAsync(commentId, cancellationToken)
                ?? throw new NotFoundException("Comment not found");

            if (!await _accessService.CanAccessTask(comment.TaskId, userId, cancellationToken: cancellationToken))
                throw new ForbiddenException("Ban khong co quyen thao tac voi binh luan nay.");

            emoji = emoji.Trim();
            if (emoji.Length is 0 or > 32)
                throw new BusinessException("Bieu cam khong hop le.");

            var existing = await _reactionRepo.Query()
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId, cancellationToken);

            if (existing == null)
            {
                await _reactionRepo.AddAsync(new CommentReaction
                {
                    Id = Guid.NewGuid(),
                    CommentId = commentId,
                    UserId = userId,
                    Emoji = emoji
                }, cancellationToken);
            }
            else if (existing.Emoji == emoji)
            {
                _reactionRepo.Delete(existing);
            }
            else
            {
                existing.Emoji = emoji;
                _reactionRepo.Update(existing);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _realtimeNotifier.ReactionChangedAsync(
                comment.TaskId,
                commentId,
                cancellationToken);
        }

        public async Task Delete(
            Guid commentId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var comment = await _repo.GetByIdAsync(commentId, cancellationToken)
                ?? throw new NotFoundException("Comment not found");

            if (comment.UserId != userId && !await _accessService.CanAccessTask(
                    comment.TaskId,
                    userId,
                    managementOnly: true,
                    cancellationToken))
                throw new ForbiddenException("Ban khong co quyen xoa binh luan nay.");

            comment.IsDeleted = true;
            _repo.Update(comment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task NotifyCommentRecipients(
            Guid taskId,
            Guid senderId,
            CancellationToken cancellationToken)
        {
            var task = await _taskRepo.GetByIdAsync(taskId, cancellationToken);
            var sender = await _userRepo.GetByIdAsync(senderId, cancellationToken);
            if (task == null || sender == null) return;

            var recipients = await _workflowService.ResolveTaskRecipients(taskId, cancellationToken);
            if (task.CreatedBy != senderId)
                recipients.Add(task.CreatedBy);

            foreach (var recipientId in recipients.Where(id => id != senderId).Distinct())
                await _notificationService.AddNotification(
                    recipientId,
                    $"{sender.FullName} da binh luan trong cong viec: {task.Title}",
                    cancellationToken);
        }
    }
}
