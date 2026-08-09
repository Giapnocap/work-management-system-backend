using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class ProgressConfiguration : IEntityTypeConfiguration<Progress>
{
    public void Configure(EntityTypeBuilder<Progress> builder)
    {
        builder.HasQueryFilter(progress => !progress.Task!.IsDeleted);
        builder.Property(progress => progress.HoursSpent).HasPrecision(18, 2);
        builder.Property(progress => progress.RowVersion).IsRowVersion();
        builder.HasAlternateKey(progress => new { progress.Id, progress.TaskId });
        builder.HasIndex(progress => new { progress.TaskId, progress.UpdatedAt });
        builder.HasIndex(progress => new
        {
            progress.UserId,
            progress.Status,
            progress.UpdatedAt,
            progress.TaskId
        });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Progress_Percent_Range", "[Percent] >= 0 AND [Percent] <= 100");
            table.HasCheckConstraint("CK_Progress_HoursSpent_NonNegative", "[HoursSpent] >= 0");
        });

        builder.HasOne(progress => progress.Task)
            .WithMany()
            .HasForeignKey(progress => progress.TaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
