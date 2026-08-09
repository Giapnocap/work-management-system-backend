using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class KpiResultConfiguration : IEntityTypeConfiguration<KpiResult>
{
    public void Configure(EntityTypeBuilder<KpiResult> builder)
    {
        builder.HasIndex(result => new { result.PeriodId, result.UserId }).IsUnique();
        builder.HasIndex(result => new { result.PeriodId, result.UnitId });
        builder.Property(result => result.FullNameSnapshot).HasMaxLength(200);
        builder.Property(result => result.EmployeeCodeSnapshot).HasMaxLength(50);
        builder.Property(result => result.UnitNameSnapshot).HasMaxLength(200);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_KpiResults_Effective_Range", "[EffectiveTo] >= [EffectiveFrom]");
            table.HasCheckConstraint(
                "CK_KpiResults_NonNegative",
                "[Score] >= 0 AND [TotalTasks] >= 0 AND [CompletedOnTime] >= 0 AND [CompletedLate] >= 0 AND [OverdueTasks] >= 0 AND [RejectedReports] >= 0 AND [BonusPoints] >= 0 AND [PenaltyPoints] >= 0 AND [ReviewPenaltyPoints] >= 0 AND [UnitAverageScore] >= 0 AND [PersonalScore] >= 0");
        });

        builder.HasOne(result => result.Period)
            .WithMany()
            .HasForeignKey(result => result.PeriodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(result => result.User)
            .WithMany()
            .HasForeignKey(result => result.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(result => result.Unit)
            .WithMany()
            .HasForeignKey(result => result.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
