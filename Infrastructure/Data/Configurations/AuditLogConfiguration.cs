using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.OccurredAt });
        builder.HasIndex(log => new { log.ActorUserId, log.OccurredAt });
        builder.Property(log => log.EntityType).HasMaxLength(64);
        builder.Property(log => log.Action).HasMaxLength(64);

        builder.HasOne(log => log.ActorUser)
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
