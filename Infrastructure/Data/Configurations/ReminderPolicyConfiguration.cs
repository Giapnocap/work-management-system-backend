using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class ReminderPolicyConfiguration : IEntityTypeConfiguration<ReminderPolicy>
{
    public void Configure(EntityTypeBuilder<ReminderPolicy> builder)
    {
        builder.Property(policy => policy.RowVersion).IsRowVersion();
        builder.HasIndex(policy => policy.ScopeType)
            .IsUnique()
            .HasFilter("[ScopeType] = 0");
        builder.HasIndex(policy => policy.UnitId)
            .IsUnique()
            .HasFilter("[ScopeType] = 1 AND [UnitId] IS NOT NULL");
        builder.HasIndex(policy => policy.ProjectId)
            .IsUnique()
            .HasFilter("[ScopeType] = 2 AND [ProjectId] IS NOT NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ReminderPolicies_ScopeType_Range",
                "[ScopeType] >= 0 AND [ScopeType] <= 2");
            table.HasCheckConstraint(
                "CK_ReminderPolicies_Scope_Shape",
                "([ScopeType] = 0 AND [UnitId] IS NULL AND [ProjectId] IS NULL) OR " +
                "([ScopeType] = 1 AND [UnitId] IS NOT NULL AND [ProjectId] IS NULL) OR " +
                "([ScopeType] = 2 AND [UnitId] IS NULL AND [ProjectId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ReminderPolicies_BeforeDueHours_Range",
                "[BeforeDueHours] >= 1 AND [BeforeDueHours] <= 720");
            table.HasCheckConstraint(
                "CK_ReminderPolicies_OverdueEscalationHours_Range",
                "[OverdueEscalationHours] >= 0 AND [OverdueEscalationHours] <= 720");
        });

        builder.HasOne(policy => policy.Unit)
            .WithMany()
            .HasForeignKey(policy => policy.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(policy => policy.Project)
            .WithMany()
            .HasForeignKey(policy => policy.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
