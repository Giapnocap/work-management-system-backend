using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Security;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class PasswordSecurityTests
{
    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void PasswordPolicy_RejectsWeakPasswords(string password)
    {
        Assert.NotNull(PasswordPolicy.GetValidationError(password));
    }

    [Fact]
    public void PasswordPolicy_RejectsPasswordPastBcryptUtf8Boundary()
    {
        var password = $"Aa1{new string('á', 35)}";

        Assert.NotNull(PasswordPolicy.GetValidationError(password));
    }

    [Fact]
    public void PasswordHashService_HashesWithConfiguredWorkFactor()
    {
        var service = new BcryptPasswordHashService();

        var hash = service.Hash("Password123");

        Assert.True(service.Verify("Password123", hash));
        Assert.False(service.NeedsRehash(hash));
        Assert.True(BCrypt.Net.BCrypt.PasswordNeedsRehash(
            hash,
            BcryptPasswordHashService.WorkFactor + 1));
    }

    [Fact]
    public async Task Login_WithLegacyHash_UpgradesWorkFactor()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = CreateUser(BCrypt.Net.BCrypt.HashPassword("Password123", workFactor: 10));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await service.Login(user.Username, "Password123");

        var persistedHash = (await context.Users.SingleAsync()).PasswordHash;
        Assert.False(BCrypt.Net.BCrypt.PasswordNeedsRehash(
            persistedHash,
            BcryptPasswordHashService.WorkFactor));
    }

    [Fact]
    public async Task Login_WithMalformedHash_ReturnsInvalidCredentials()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = CreateUser("not-a-bcrypt-hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.Login(user.Username, "Password123"));
    }

    [Fact]
    public async Task ChangePassword_RejectsCurrentPassword()
    {
        await using var context = TestFactory.CreateDbContext();
        var hasher = TestFactory.CreatePasswordHashService();
        var user = CreateUser(hasher.Hash("Password123"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new ChangePasswordService(
            context,
            TestFactory.CreateAuditService(context),
            hasher);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ChangePassword(user.Id, new ChangePasswordDto
            {
                OldPassword = "Password123",
                NewPassword = "Password123",
                ConfirmPassword = "Password123"
            }));

        Assert.Equal(0, user.TokenVersion);
        Assert.Empty(context.AuditLogs);
    }

    private static User CreateUser(string passwordHash)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = $"security-{Guid.NewGuid():N}",
            FullName = "Security User",
            EmployeeCode = $"SEC{Guid.NewGuid():N}"[..12],
            PasswordHash = passwordHash,
            Role = SystemRoles.User,
            IsApproved = true
        };
}
