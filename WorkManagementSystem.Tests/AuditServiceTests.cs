using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class AuditServiceTests
{
    [Fact]
    public async Task RecordAsync_PersistsStructuredEventAndSupportsFiltering()
    {
        await using var context = TestFactory.CreateDbContext();
        var actor = CreateUser("admin", "Admin");
        var entityId = Guid.NewGuid();
        context.Users.Add(actor);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuditService(context);

        await service.RecordAsync(
            AuditEntityTypes.Project,
            entityId,
            AuditActions.Created,
            actor.Id,
            new { Name = "Internal portal", UnitId = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var result = await service.GetAsync(
            AuditEntityTypes.Project,
            entityId,
            AuditActions.Created,
            actor.Id,
            null,
            null,
            1,
            20);

        var audit = Assert.Single(result.Data);
        Assert.Equal(1, result.Total);
        Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.Contains("\"name\":\"Internal portal\"", audit.DetailsJson);
    }

    [Fact]
    public async Task ChangePassword_AuditDoesNotStorePasswordValues()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = CreateUser("employee", "User");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new ChangePasswordService(context, TestFactory.CreateAuditService(context));

        await service.ChangePassword(user.Id, new ChangePasswordDto
        {
            OldPassword = "Password@123",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });

        var audit = await context.AuditLogs.SingleAsync();
        Assert.Equal(AuditActions.PasswordChanged, audit.Action);
        Assert.Equal(user.Id, audit.ActorUserId);
        Assert.Null(audit.DetailsJson);
    }

    private static User CreateUser(string username, string role)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = $"{role[..1].ToUpperInvariant()}-{Guid.NewGuid():N}"[..12],
            PasswordHash = "hash",
            Role = role,
            IsApproved = true
        };
    }
}
