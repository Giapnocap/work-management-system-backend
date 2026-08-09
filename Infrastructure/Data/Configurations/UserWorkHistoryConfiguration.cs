using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UserWorkHistoryConfiguration : IEntityTypeConfiguration<UserWorkHistory>
{
    public void Configure(EntityTypeBuilder<UserWorkHistory> builder)
    {
        builder.HasIndex(history => new { history.UserId, history.EffectiveFrom });
        builder.HasIndex(history => new { history.UnitId, history.EffectiveFrom });
        builder.HasIndex(history => history.UserId)
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL");

        builder.HasOne(history => history.User)
            .WithMany()
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(history => history.Unit)
            .WithMany()
            .HasForeignKey(history => history.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(history => history.ChangedByUser)
            .WithMany()
            .HasForeignKey(history => history.ChangedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
