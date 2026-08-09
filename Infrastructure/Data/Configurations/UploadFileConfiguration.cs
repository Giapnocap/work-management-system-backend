using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class UploadFileConfiguration : IEntityTypeConfiguration<UploadFile>
{
    public void Configure(EntityTypeBuilder<UploadFile> builder)
    {
        builder.Property(file => file.FileName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(file => file.StorageKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.TaskId).IsRequired();

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(file => file.TaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Progress>()
            .WithMany()
            .HasForeignKey(file => new { file.ProgressId, file.TaskId })
            .HasPrincipalKey(progress => new { progress.Id, progress.TaskId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(file => file.UploadedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
