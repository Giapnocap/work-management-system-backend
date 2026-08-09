using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasQueryFilter(user => !user.IsDeleted);
        builder.Property(user => user.RowVersion).IsRowVersion();
        builder.HasIndex(user => user.Username).IsUnique();
        builder.HasIndex(user => user.EmployeeCode).IsUnique();

        builder.HasOne(user => user.Unit)
            .WithMany()
            .HasForeignKey(user => user.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
