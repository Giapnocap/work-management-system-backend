using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class RecurringTaskTemplateConfiguration : IEntityTypeConfiguration<RecurringTaskTemplate>
{
    public void Configure(EntityTypeBuilder<RecurringTaskTemplate> builder)
    {
        builder.HasQueryFilter(template => !template.IsDeleted);
        builder.Property(template => template.Title).HasMaxLength(200).IsRequired();
        builder.Property(template => template.Description).HasMaxLength(1000).IsRequired();
        builder.Property(template => template.PlannedEffortHours).HasPrecision(18, 2);
        builder.Property(template => template.UnitId).IsRequired();
        builder.Property(template => template.RowVersion).IsRowVersion();

        builder.HasIndex(template => new { template.IsActive, template.NextRunAtUtc });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_RecurringTaskTemplates_Interval_Positive",
                "[Interval] >= 1");
            table.HasCheckConstraint(
                "CK_RecurringTaskTemplates_PlannedEffortHours_Positive",
                "[PlannedEffortHours] IS NULL OR [PlannedEffortHours] > 0");
            table.HasCheckConstraint(
                "CK_RecurringTaskTemplates_RecurrenceType_Range",
                "[RecurrenceType] >= 0 AND [RecurrenceType] <= 2");
            table.HasCheckConstraint(
                "CK_RecurringTaskTemplates_Schedule_Shape",
                "([RecurrenceType] = 0 AND [DayOfWeek] IS NULL AND [DayOfMonth] IS NULL) OR " +
                "([RecurrenceType] = 1 AND [DayOfWeek] BETWEEN 0 AND 6 AND [DayOfMonth] IS NULL) OR " +
                "([RecurrenceType] = 2 AND [DayOfWeek] IS NULL AND [DayOfMonth] BETWEEN 1 AND 31)");
        });

        builder.HasOne(template => template.Unit)
            .WithMany()
            .HasForeignKey(template => template.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(template => template.Project)
            .WithMany()
            .HasForeignKey(template => new { template.ProjectId, template.UnitId })
            .HasPrincipalKey(project => new { project.Id, project.UnitId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(template => template.CreatedByUser)
            .WithMany()
            .HasForeignKey(template => template.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
