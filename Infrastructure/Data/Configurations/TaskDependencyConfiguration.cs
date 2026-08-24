using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.HasIndex(dependency => new { dependency.TaskId, dependency.DependsOnTaskId })
            .IsUnique();
        builder.HasIndex(dependency => dependency.DependsOnTaskId);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_TaskDependencies_NoSelfReference",
                "[TaskId] <> [DependsOnTaskId]"));

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(dependency => dependency.TaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(dependency => dependency.DependsOnTaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(dependency => dependency.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
