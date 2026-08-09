using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class CommentSeenConfiguration : IEntityTypeConfiguration<CommentSeen>
{
    public void Configure(EntityTypeBuilder<CommentSeen> builder)
        => builder.HasIndex(seen => new { seen.CommentId, seen.UserId }).IsUnique();
}
