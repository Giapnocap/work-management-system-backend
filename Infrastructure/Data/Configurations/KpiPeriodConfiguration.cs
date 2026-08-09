using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class KpiPeriodConfiguration : IEntityTypeConfiguration<KpiPeriod>
{
    public void Configure(EntityTypeBuilder<KpiPeriod> builder)
    {
        builder.Property(period => period.RowVersion).IsRowVersion();
        builder.HasIndex(period => new { period.StartDate, period.EndDate }).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_KpiPeriods_Date_Range", "[EndDate] > [StartDate]");
        });

        builder.HasOne(period => period.Locker)
            .WithMany()
            .HasForeignKey(period => period.LockedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
