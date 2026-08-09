using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Unit> Units { get; }
        DbSet<UserUnit> UserUnits { get; }
        DbSet<TaskItem> Tasks { get; }
        DbSet<TaskAssignee> TaskAssignees { get; }
        DbSet<Progress> Progresses { get; }
        DbSet<UploadFile> UploadFiles { get; }
        DbSet<ReportReview> Reviews { get; }
        DbSet<Notification> Notifications { get; }
        DbSet<TaskHistory> TaskHistories { get; }
        DbSet<TaskComment> TaskComments { get; }
        DbSet<CommentReaction> CommentReactions { get; }
        DbSet<CommentSeen> CommentSeens { get; }
        DbSet<SubTask> SubTasks { get; }
        DbSet<Project> Projects { get; }
        DbSet<KpiPeriod> KpiPeriods { get; }
        DbSet<KpiResult> KpiResults { get; }
        DbSet<UserWorkHistory> UserWorkHistories { get; }
        DbSet<AuditLog> AuditLogs { get; }

        void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion)
            where TEntity : class, IHasRowVersion;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
