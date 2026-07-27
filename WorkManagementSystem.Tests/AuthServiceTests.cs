using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Register_HashesPassword_AndCreatesPendingUser()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = TestFactory.CreateAuthService(context);

        var result = await service.Register(new AuthDto
        {
            Username = "intern01",
            Password = "Password@123",
            FullName = "Intern One",
            PhoneNumber = "0900000000"
        });

        var user = await context.Users.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.False(user.IsApproved);
        Assert.NotEqual("Password@123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password@123", user.PasswordHash));
        Assert.Equal("EMP0001", user.EmployeeCode);
    }

    [Fact]
    public async Task Register_UsesNextEmployeeCodeAcrossActiveAndDeletedUsers()
    {
        await using var context = TestFactory.CreateDbContext();
        context.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                Username = "existing",
                PasswordHash = "hash",
                FullName = "Existing",
                EmployeeCode = "EMP0010"
            },
            new User
            {
                Id = Guid.NewGuid(),
                Username = "deleted",
                PasswordHash = "hash",
                FullName = "Deleted",
                EmployeeCode = "EMP0012",
                IsDeleted = true
            });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await service.Register(new AuthDto
        {
            Username = "next-employee",
            Password = "Password@123",
            FullName = "Next Employee"
        });

        var user = await context.Users.SingleAsync(candidate => candidate.Username == "next-employee");
        Assert.Equal("EMP0013", user.EmployeeCode);
    }

    [Fact]
    public void Model_DefinesEmployeeCodeSequence()
    {
        using var context = TestFactory.CreateDbContext();

        var sequence = context.Model.FindSequence(EmployeeCodeGenerator.SequenceName);

        Assert.NotNull(sequence);
        Assert.Equal(1, sequence.StartValue);
        Assert.Equal(1, sequence.IncrementBy);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "duplicate",
            PasswordHash = "hash",
            FullName = "Existing",
            EmployeeCode = "EMP0001"
        });
        await context.SaveChangesAsync();

        var service = TestFactory.CreateAuthService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Register(new AuthDto
        {
            Username = "duplicate",
            Password = "Password@123",
            FullName = "Duplicate User"
        }));
    }

    [Fact]
    public async Task Register_WithUnknownUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = TestFactory.CreateAuthService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Register(new AuthDto
        {
            Username = "unknown-unit",
            Password = "Password@123",
            FullName = "Unknown Unit",
            UnitId = Guid.NewGuid()
        }));

        Assert.Empty(await context.Users.ToListAsync());
    }

    [Fact]
    public async Task ApproveUser_WithUnknownUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "pending-unit",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Pending Unit",
            EmployeeCode = "EMP0001",
            UnitId = Guid.NewGuid(),
            IsApproved = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.ApproveUser(user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.False(savedUser.IsApproved);
        Assert.Empty(await context.UserUnits.ToListAsync());
    }

    [Fact]
    public async Task ApproveUser_InvalidatesAnyPreviouslyIssuedSessionState()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "pending-approval",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Pending Approval",
            EmployeeCode = "EMP0099",
            Role = "User",
            IsApproved = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await service.ApproveUser(user.Id);

        Assert.True(user.IsApproved);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task Login_WithPendingUser_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "pending",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Pending User",
            EmployeeCode = "EMP0001",
            IsApproved = false
        });
        await context.SaveChangesAsync();

        var service = TestFactory.CreateAuthService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Login("pending", "Password@123"));
    }

    [Fact]
    public async Task Login_WithUnknownUserOrWrongPassword_ReturnsSameAuthenticationError()
    {
        await using var context = TestFactory.CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "approved",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Approved User",
            EmployeeCode = "EMP0001",
            Role = "User",
            IsApproved = true
        });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        var unknownUser = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.Login("unknown", "Password@123"));
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.Login("approved", "wrong-password"));

        Assert.Equal(unknownUser.Code, wrongPassword.Code);
        Assert.Equal(unknownUser.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Login_WithApprovedUser_ReturnsJwtToken()
    {
        await using var context = TestFactory.CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "approved",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Approved User",
            EmployeeCode = "EMP0001",
            Role = "User",
            IsApproved = true,
            TokenVersion = 7
        });
        await context.SaveChangesAsync();

        var service = TestFactory.CreateAuthService(context);

        var token = await service.Login("approved", "Password@123");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);

        var payloadBytes = Base64UrlEncoder.DecodeBytes(token.Split('.')[1]);
        using var payload = JsonDocument.Parse(payloadBytes);
        Assert.Equal("WorkManagementSystem.Tests", payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal("WorkManagementSystem.Tests.Client", payload.RootElement.GetProperty("aud").GetString());
        Assert.Equal("7", payload.RootElement.GetProperty("token_version").GetString());
        Assert.True(payload.RootElement.TryGetProperty(JwtRegisteredClaimNames.Jti, out _));
    }

    [Fact]
    public async Task GetPendingUsers_WhenRequestIsCancelled_StopsDatabaseQuery()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = TestFactory.CreateAuthService(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetPendingUsers(cancellation.Token));
    }

    [Fact]
    public async Task ResetPassword_InvalidatesExistingSessions()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "reset-user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Reset User",
            EmployeeCode = "EMP0002",
            Role = "User",
            IsApproved = true,
            TokenVersion = 3
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await service.ResetPassword(new ResetPasswordDto
        {
            Username = user.Username,
            NewPassword = "NewPassword@123"
        });

        Assert.Equal(4, user.TokenVersion);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword@123", user.PasswordHash));
    }

    [Fact]
    public async Task RejectUser_InvalidatesSessionsAndSoftDeletesAccount()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "rejected-user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            FullName = "Rejected User",
            EmployeeCode = "EMP0003",
            Role = "User",
            IsApproved = false,
            TokenVersion = 2
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateAuthService(context);

        await service.RejectUser(user.Id);

        var savedUser = await context.Users.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == user.Id);
        Assert.True(savedUser.IsDeleted);
        Assert.Equal(3, savedUser.TokenVersion);
    }
}
