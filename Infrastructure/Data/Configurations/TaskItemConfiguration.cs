using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasQueryFilter(task => !task.IsDeleted);
        builder.Property(task => task.ActualHours).HasPrecision(18, 2);
        builder.Property(task => task.RowVersion).IsRowVersion();
        builder.Property(task => task.UnitId).IsRequired();
        builder.HasIndex(task => new { task.ProjectId, task.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Tasks_ActualHours_NonNegative", "[ActualHours] >= 0");
            table.HasCheckConstraint("CK_Tasks_Status_Range", "[Status] >= 0 AND [Status] <= 3");
            table.HasCheckConstraint(
                "CK_Tasks_Date_Range",
                "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");
        });

        builder.HasOne(task => task.Creator)
            .WithMany()
            .HasForeignKey(task => task.CreatedBy)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(task => task.Unit)
            .WithMany()
            .HasForeignKey(task => task.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(task => task.Project)
            .WithMany()
            .HasForeignKey(task => new { task.ProjectId, task.UnitId })
            .HasPrincipalKey(project => new { project.Id, project.UnitId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
