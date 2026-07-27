using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<long>("EmployeeCodeSequence")
                .StartsAt(1)
                .IncrementsBy(1);

            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<TaskItem>().HasQueryFilter(t => !t.IsDeleted);
            modelBuilder.Entity<Unit>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<TaskComment>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsArchived);
            modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.User!.IsDeleted);
            modelBuilder.Entity<TaskAssignee>().HasQueryFilter(a => !a.Task!.IsDeleted);
            modelBuilder.Entity<Progress>().HasQueryFilter(p => !p.Task!.IsDeleted);
            modelBuilder.Entity<ReportReview>().HasQueryFilter(r => !r.Progress!.Task!.IsDeleted);
            modelBuilder.Entity<UserUnit>().HasQueryFilter(uu => !uu.User!.IsDeleted && !uu.Unit!.IsDeleted);

            modelBuilder.Entity<TaskItem>().Property(t => t.ActualHours).HasPrecision(18, 2);
            modelBuilder.Entity<Progress>().Property(p => p.HoursSpent).HasPrecision(18, 2);
            modelBuilder.Entity<TaskItem>().Property(t => t.RowVersion).IsRowVersion();
            modelBuilder.Entity<Progress>().Property(p => p.RowVersion).IsRowVersion();
            modelBuilder.Entity<User>().Property(u => u.RowVersion).IsRowVersion();
            modelBuilder.Entity<Unit>().Property(u => u.RowVersion).IsRowVersion();
            modelBuilder.Entity<Project>().Property(p => p.RowVersion).IsRowVersion();
            modelBuilder.Entity<KpiPeriod>().Property(p => p.RowVersion).IsRowVersion();
            modelBuilder.Entity<Project>().Property(p => p.UnitId).IsRequired();
            modelBuilder.Entity<TaskItem>().Property(t => t.UnitId).IsRequired();
            modelBuilder.Entity<UploadFile>().Property(f => f.TaskId).IsRequired();

            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.EmployeeCode).IsUnique();
            modelBuilder.Entity<Unit>().HasIndex(u => u.Name).IsUnique();
            modelBuilder.Entity<UserUnit>().HasIndex(uu => uu.UserId).IsUnique();
            modelBuilder.Entity<TaskAssignee>().HasIndex(ta => new { ta.TaskId, ta.UserId }).IsUnique();
            modelBuilder.Entity<TaskAssignee>().HasIndex(ta => new { ta.TaskId, ta.UnitId }).IsUnique();
            modelBuilder.Entity<SubTask>().HasIndex(st => new { st.TaskId, st.Title }).IsUnique();
            modelBuilder.Entity<ReportReview>().HasIndex(r => r.ProgressId).IsUnique();
            modelBuilder.Entity<CommentReaction>().HasIndex(r => new { r.CommentId, r.UserId }).IsUnique();
            modelBuilder.Entity<CommentSeen>().HasIndex(s => new { s.CommentId, s.UserId }).IsUnique();
            modelBuilder.Entity<Project>().HasIndex(p => new { p.UnitId, p.Name }).IsUnique();
            modelBuilder.Entity<Project>().HasAlternateKey(p => new { p.Id, p.UnitId });
            modelBuilder.Entity<Progress>().HasAlternateKey(p => new { p.Id, p.TaskId });
            modelBuilder.Entity<TaskItem>().HasIndex(t => new { t.ProjectId, t.Status });
            modelBuilder.Entity<KpiPeriod>().HasIndex(p => new { p.StartDate, p.EndDate }).IsUnique();
            modelBuilder.Entity<KpiResult>().HasIndex(r => new { r.PeriodId, r.UserId }).IsUnique();
            modelBuilder.Entity<UserWorkHistory>().HasIndex(h => new { h.UserId, h.EffectiveFrom });
            modelBuilder.Entity<TaskHistory>().HasIndex(h => new { h.TaskId, h.ChangedAt });
            modelBuilder.Entity<AuditLog>().HasIndex(log => new { log.EntityType, log.EntityId, log.OccurredAt });
            modelBuilder.Entity<AuditLog>().HasIndex(log => new { log.ActorUserId, log.OccurredAt });
            modelBuilder.Entity<AuditLog>().Property(log => log.EntityType).HasMaxLength(64);
            modelBuilder.Entity<AuditLog>().Property(log => log.Action).HasMaxLength(64);
            modelBuilder.Entity<KpiResult>().Property(result => result.FullNameSnapshot).HasMaxLength(200);
            modelBuilder.Entity<KpiResult>().Property(result => result.EmployeeCodeSnapshot).HasMaxLength(50);
            modelBuilder.Entity<KpiResult>().Property(result => result.UnitNameSnapshot).HasMaxLength(200);
            modelBuilder.Entity<UserWorkHistory>()
                .HasIndex(h => h.UserId)
                .IsUnique()
                .HasFilter("[EffectiveTo] IS NULL");

            modelBuilder.Entity<Progress>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_Progress_Percent_Range", "[Percent] >= 0 AND [Percent] <= 100");
                t.HasCheckConstraint("CK_Progress_HoursSpent_NonNegative", "[HoursSpent] >= 0");
            });

            modelBuilder.Entity<TaskItem>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_Tasks_ActualHours_NonNegative", "[ActualHours] >= 0");
                t.HasCheckConstraint("CK_Tasks_Status_Range", "[Status] >= 0 AND [Status] <= 3");
                t.HasCheckConstraint(
                    "CK_Tasks_Date_Range",
                    "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");
            });

            modelBuilder.Entity<TaskAssignee>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_TaskAssignee_One_Target", "([UserId] IS NOT NULL AND [UnitId] IS NULL) OR ([UserId] IS NULL AND [UnitId] IS NOT NULL)");
            });

            modelBuilder.Entity<KpiPeriod>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_KpiPeriods_Date_Range", "[EndDate] > [StartDate]");
            });

            modelBuilder.Entity<KpiResult>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_KpiResults_Effective_Range", "[EffectiveTo] >= [EffectiveFrom]");
                t.HasCheckConstraint(
                    "CK_KpiResults_NonNegative",
                    "[Score] >= 0 AND [TotalTasks] >= 0 AND [CompletedOnTime] >= 0 AND [CompletedLate] >= 0 AND [OverdueTasks] >= 0 AND [RejectedReports] >= 0 AND [BonusPoints] >= 0 AND [PenaltyPoints] >= 0 AND [ReviewPenaltyPoints] >= 0 AND [UnitAverageScore] >= 0 AND [PersonalScore] >= 0");
            });

            modelBuilder.Entity<User>()
                .HasOne(u => u.Unit)
                .WithMany()
                .HasForeignKey(u => u.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Creator)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Unit)
                .WithMany()
                .HasForeignKey(t => t.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Project)
                .WithMany()
                .HasForeignKey(t => new { t.ProjectId, t.UnitId })
                .HasPrincipalKey(p => new { p.Id, p.UnitId })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Progress>()
                .HasOne(p => p.Task)
                .WithMany()
                .HasForeignKey(p => p.TaskId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Progress>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.Task)
                .WithMany()
                .HasForeignKey(ta => ta.TaskId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.User)
                .WithMany()
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.Unit)
                .WithMany()
                .HasForeignKey(ta => ta.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReportReview>()
                .HasOne(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReportReview>()
                .HasOne(r => r.Progress)
                .WithMany()
                .HasForeignKey(r => r.ProgressId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Unit)
                .WithMany()
                .HasForeignKey(p => p.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<KpiPeriod>()
                .HasOne(p => p.Locker)
                .WithMany()
                .HasForeignKey(p => p.LockedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<KpiResult>()
                .HasOne(r => r.Period)
                .WithMany()
                .HasForeignKey(r => r.PeriodId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<KpiResult>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<KpiResult>()
                .HasOne(r => r.Unit)
                .WithMany()
                .HasForeignKey(r => r.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserWorkHistory>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserWorkHistory>()
                .HasOne(h => h.Unit)
                .WithMany()
                .HasForeignKey(h => h.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserWorkHistory>()
                .HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskHistory>()
                .HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskHistory>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(h => h.ChangedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AuditLog>()
                .HasOne(log => log.ActorUser)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UploadFile>()
                .HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(file => file.TaskId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UploadFile>()
                .HasOne<Progress>()
                .WithMany()
                .HasForeignKey(file => new { file.ProgressId, file.TaskId })
                .HasPrincipalKey(progress => new { progress.Id, progress.TaskId })
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UploadFile>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(file => file.UploadedBy)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
