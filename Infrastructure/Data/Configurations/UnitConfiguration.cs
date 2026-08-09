using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.HasQueryFilter(unit => !unit.IsDeleted);
        builder.Property(unit => unit.RowVersion).IsRowVersion();
        builder.HasIndex(unit => unit.Name).IsUnique();
    }
}
