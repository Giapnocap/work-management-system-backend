using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UserCapacityConfiguration : IEntityTypeConfiguration<UserCapacity>
{
    public void Configure(EntityTypeBuilder<UserCapacity> builder)
    {
        builder.Property(capacity => capacity.WeeklyCapacityHours).HasPrecision(18, 2);
        builder.HasIndex(capacity => new { capacity.UserId, capacity.EffectiveFrom });
        builder.HasIndex(capacity => capacity.UserId)
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_UserCapacities_WeeklyHours_Positive",
                "[WeeklyCapacityHours] > 0");
            table.HasCheckConstraint(
                "CK_UserCapacities_Effective_Range",
                "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
        });

        builder.HasOne(capacity => capacity.User)
            .WithMany()
            .HasForeignKey(capacity => capacity.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(capacity => capacity.ChangedByUser)
            .WithMany()
            .HasForeignKey(capacity => capacity.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
