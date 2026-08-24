using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        builder.Property(history => history.FieldName)
            .IsRequired();
        builder.Property(history => history.Reason)
            .HasMaxLength(1000);
        builder.HasIndex(history => new { history.TaskId, history.ChangedAt, history.Id });

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(history => history.TaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(history => history.ChangedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
