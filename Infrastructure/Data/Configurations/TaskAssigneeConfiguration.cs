using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class TaskAssigneeConfiguration : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> builder)
    {
        builder.HasQueryFilter(assignee => !assignee.Task!.IsDeleted);
        builder.HasIndex(assignee => new { assignee.TaskId, assignee.UserId }).IsUnique();
        builder.HasIndex(assignee => new { assignee.TaskId, assignee.UnitId }).IsUnique();
        builder.HasIndex(assignee => new { assignee.UserId, assignee.TaskId });
        builder.HasIndex(assignee => new { assignee.UnitId, assignee.TaskId });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_TaskAssignee_One_Target",
                "([UserId] IS NOT NULL AND [UnitId] IS NULL) OR ([UserId] IS NULL AND [UnitId] IS NOT NULL)");
        });

        builder.HasOne(assignee => assignee.Task)
            .WithMany()
            .HasForeignKey(assignee => assignee.TaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(assignee => assignee.User)
            .WithMany()
            .HasForeignKey(assignee => assignee.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(assignee => assignee.Unit)
            .WithMany()
            .HasForeignKey(assignee => assignee.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
