using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasQueryFilter(project => !project.IsArchived);
        builder.Property(project => project.RowVersion).IsRowVersion();
        builder.Property(project => project.UnitId).IsRequired();
        builder.HasIndex(project => new { project.UnitId, project.Name }).IsUnique();
        builder.HasAlternateKey(project => new { project.Id, project.UnitId });

        builder.HasOne(project => project.Unit)
            .WithMany()
            .HasForeignKey(project => project.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(project => project.Creator)
            .WithMany()
            .HasForeignKey(project => project.CreatedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
