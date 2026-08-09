using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<UserUnit> UserUnits { get; set; }
    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<TaskAssignee> TaskAssignees { get; set; }
    public DbSet<Progress> Progresses { get; set; }
    public DbSet<UploadFile> UploadFiles { get; set; }
    public DbSet<ReportReview> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<TaskHistory> TaskHistories { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }
    public DbSet<CommentReaction> CommentReactions { get; set; }
    public DbSet<CommentSeen> CommentSeens { get; set; }
    public DbSet<SubTask> SubTasks { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<KpiPeriod> KpiPeriods { get; set; }
    public DbSet<KpiResult> KpiResults { get; set; }
    public DbSet<UserWorkHistory> UserWorkHistories { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion)
        where TEntity : class, IHasRowVersion
    {
        Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion.ToArray();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("EmployeeCodeSequence")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
