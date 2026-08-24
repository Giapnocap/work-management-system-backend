using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data.Configurations;

public sealed class RecurringTaskAssigneeConfiguration : IEntityTypeConfiguration<RecurringTaskAssignee>
{
    public void Configure(EntityTypeBuilder<RecurringTaskAssignee> builder)
    {
        builder.HasIndex(assignee => new { assignee.TemplateId, assignee.UserId }).IsUnique();
        builder.HasIndex(assignee => new { assignee.UserId, assignee.TemplateId });

        builder.HasOne(assignee => assignee.Template)
            .WithMany(template => template.Assignees)
            .HasForeignKey(assignee => assignee.TemplateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(assignee => assignee.User)
            .WithMany()
            .HasForeignKey(assignee => assignee.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
