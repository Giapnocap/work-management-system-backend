using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UserUnitConfiguration : IEntityTypeConfiguration<UserUnit>
{
    public void Configure(EntityTypeBuilder<UserUnit> builder)
    {
        builder.HasQueryFilter(userUnit =>
            !userUnit.User!.IsDeleted && !userUnit.Unit!.IsDeleted);
        builder.HasIndex(userUnit => userUnit.UserId).IsUnique();
    }
}
