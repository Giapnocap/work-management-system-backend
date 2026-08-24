using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class GeneratedTaskOccurrenceConfiguration : IEntityTypeConfiguration<GeneratedTaskOccurrence>
{
    public void Configure(EntityTypeBuilder<GeneratedTaskOccurrence> builder)
    {
        builder.HasKey(occurrence => new { occurrence.TemplateId, occurrence.ScheduledForUtc });
        builder.HasIndex(occurrence => occurrence.TaskId).IsUnique();

        builder.HasOne(occurrence => occurrence.Template)
            .WithMany(template => template.Occurrences)
            .HasForeignKey(occurrence => occurrence.TemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(occurrence => occurrence.Task)
            .WithMany()
            .HasForeignKey(occurrence => occurrence.TaskId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
