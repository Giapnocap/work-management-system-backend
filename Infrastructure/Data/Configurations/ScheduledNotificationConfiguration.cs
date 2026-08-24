using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class ScheduledNotificationConfiguration : IEntityTypeConfiguration<ScheduledNotification>
{
    public void Configure(EntityTypeBuilder<ScheduledNotification> builder)
    {
        builder.Property(notification => notification.EventKey)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(notification => notification.LastError)
            .HasMaxLength(1000);
        builder.Property(notification => notification.RowVersion).IsRowVersion();

        builder.HasIndex(notification => notification.EventKey).IsUnique();
        builder.HasIndex(notification => new { notification.TaskId, notification.Type }).IsUnique();
        builder.HasIndex(notification => new
        {
            notification.Status,
            notification.ScheduledForUtc,
            notification.RetryCount
        });
        builder.HasIndex(notification => new { notification.TaskId, notification.ScheduledForUtc });
        builder.HasIndex(notification => new { notification.TaskId, notification.SentAtUtc, notification.Id });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ScheduledNotifications_Type_Range",
                "[Type] >= 0 AND [Type] <= 2");
            table.HasCheckConstraint(
                "CK_ScheduledNotifications_Status_Range",
                "[Status] >= 0 AND [Status] <= 3");
            table.HasCheckConstraint(
                "CK_ScheduledNotifications_RetryCount_NonNegative",
                "[RetryCount] >= 0");
        });

        builder.HasOne(notification => notification.Task)
            .WithMany()
            .HasForeignKey(notification => notification.TaskId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
