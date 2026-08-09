using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class ReportReviewConfiguration : IEntityTypeConfiguration<ReportReview>
{
    public void Configure(EntityTypeBuilder<ReportReview> builder)
    {
        builder.HasQueryFilter(review => !review.Progress!.Task!.IsDeleted);
        builder.HasIndex(review => review.ProgressId).IsUnique();

        builder.HasOne(review => review.Reviewer)
            .WithMany()
            .HasForeignKey(review => review.ReviewerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(review => review.Progress)
            .WithMany()
            .HasForeignKey(review => review.ProgressId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
