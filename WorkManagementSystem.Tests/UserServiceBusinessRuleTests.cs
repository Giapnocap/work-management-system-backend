using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class UserServiceBusinessRuleTests
{
    [Fact]
    public async Task Update_PromoteUserWithPendingTask_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unit.Id);
        SeedTask(context, user.Id, admin.Id, unit.Id, "Pending task", TaskStatusEnum.InProgress);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(user.Id, new UpdateUserDto
        {
            Role = "Manager",
            UnitId = unit.Id
        }, admin.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("User", savedUser.Role);
    }

    [Fact]
    public async Task Update_TransferUserWithPendingTask_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unitA.Id);
        SeedTask(context, user.Id, admin.Id, unitA.Id, "Old unit pending task", TaskStatusEnum.NotStarted);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(user.Id, new UpdateUserDto
        {
            Role = "User",
            UnitId = unitB.Id
        }, admin.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unitA.Id, savedUser.UnitId);
    }

    [Fact]
    public async Task Update_WithUnknownUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(user.Id, new UpdateUserDto
        {
            Role = "User",
            UnitId = Guid.NewGuid()
        }, admin.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unit.Id, savedUser.UnitId);
    }

    [Fact]
    public async Task Delete_UserWithPendingTask_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unit.Id);
        SeedTask(context, user.Id, admin.Id, unit.Id, "Pending task", TaskStatusEnum.NotStarted);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Delete(user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.False(savedUser.IsDeleted);
        Assert.True(await context.TaskAssignees.AnyAsync(a => a.UserId == user.Id));
    }

    [Fact]
    public async Task Delete_UserWithoutPendingWork_InvalidatesSessionsAndSoftDeletes()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var user = SeedUser(context, "employee", "User", unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await service.Delete(user.Id);

        var savedUser = await context.Users.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == user.Id);
        Assert.True(savedUser.IsDeleted);
        Assert.Equal(1, savedUser.TokenVersion);
    }

    [Fact]
    public async Task Delete_UserWithCompletedWork_PreservesAssignmentAndClosesEmploymentHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unit.Id);
        SeedTask(context, user.Id, admin.Id, unit.Id, "Completed task", TaskStatusEnum.Approved);
        SeedMembership(context, user.Id, unit.Id);
        SeedHistory(context, user.Id, unit.Id, "User", DateTime.UtcNow.AddMonths(-1), null);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = TestFactory.CreateUserService(context, transactions);

        await service.Delete(user.Id, admin.Id);

        var savedUser = await context.Users.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == user.Id);
        var assignment = await context.TaskAssignees.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.UserId == user.Id);
        var history = await context.UserWorkHistories.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.UserId == user.Id);

        Assert.True(savedUser.IsDeleted);
        Assert.NotEqual(Guid.Empty, assignment.TaskId);
        Assert.NotNull(history.EffectiveTo);
        Assert.Empty(await context.UserUnits.IgnoreQueryFilters()
            .Where(mapping => mapping.UserId == user.Id)
            .ToListAsync());
        Assert.Equal(1, transactions.ExecutionCount);
        Assert.Equal(1, transactions.SerializableExecutionCount);
    }

    [Fact]
    public async Task Update_WhenUnitChanges_ReplacesUserUnitMembershipWithoutDuplicate()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unitA.Id);
        SeedMembership(context, user.Id, unitA.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await service.Update(user.Id, new UpdateUserDto
        {
            Role = "User",
            UnitId = unitB.Id
        }, admin.Id);

        var mappings = await context.UserUnits
            .Where(uu => uu.UserId == user.Id)
            .ToListAsync();

        var mapping = Assert.Single(mappings);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(unitB.Id, mapping.UnitId);
    }

    [Fact]
    public async Task Update_WithOldManagerTransfer_UpdatesOldManagerAssignmentAndMembership()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var admin = SeedUser(context, "admin", "Admin", null);
        var oldManager = SeedUser(context, "old-manager", "Manager", unitA.Id);
        var newManager = SeedUser(context, "new-manager", "User", unitA.Id);
        SeedMembership(context, oldManager.Id, unitA.Id);
        SeedHistory(context, oldManager.Id, unitA.Id, "Manager", DateTime.UtcNow.AddDays(-10), null);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await service.Update(newManager.Id, new UpdateUserDto
        {
            Role = "Manager",
            UnitId = unitA.Id,
            OldManagerId = oldManager.Id,
            OldManagerAction = "Transfer",
            OldManagerNewUnitId = unitB.Id
        }, admin.Id);

        var savedOldManager = await context.Users.SingleAsync(u => u.Id == oldManager.Id);
        Assert.Equal("User", savedOldManager.Role);
        Assert.Equal(unitB.Id, savedOldManager.UnitId);

        var oldManagerMapping = Assert.Single(await context.UserUnits
            .Where(uu => uu.UserId == oldManager.Id)
            .ToListAsync());
        Assert.Equal(unitB.Id, oldManagerMapping.UnitId);

        var oldManagerHistories = await context.UserWorkHistories
            .Where(h => h.UserId == oldManager.Id)
            .OrderBy(h => h.EffectiveFrom)
            .ToListAsync();
        Assert.Equal(2, oldManagerHistories.Count);
        Assert.NotNull(oldManagerHistories[0].EffectiveTo);
        Assert.Equal("User", oldManagerHistories[1].Role);
        Assert.Equal(unitB.Id, oldManagerHistories[1].UnitId);
        Assert.Null(oldManagerHistories[1].EffectiveTo);
    }

    [Fact]
    public async Task Update_PromoteIntoManagedUnitWithoutReplacementDetails_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var oldManager = SeedUser(context, "old-manager", "Manager", unit.Id);
        var candidate = SeedUser(context, "candidate", "User", unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(candidate.Id, new UpdateUserDto
        {
            Role = "Manager",
            UnitId = unit.Id
        }, admin.Id));

        Assert.Equal("Manager", (await context.Users.SingleAsync(u => u.Id == oldManager.Id)).Role);
        Assert.Equal("User", (await context.Users.SingleAsync(u => u.Id == candidate.Id)).Role);
    }

    [Fact]
    public async Task Update_ReplaceManagerWithPendingUnitWork_ThrowsWithoutPartialChanges()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var admin = SeedUser(context, "admin", "Admin", null);
        var oldManager = SeedUser(context, "old-manager", "Manager", unitA.Id);
        var candidate = SeedUser(context, "candidate", "User", unitA.Id);
        var employee = SeedUser(context, "employee", "User", unitA.Id);
        SeedMembership(context, oldManager.Id, unitA.Id);
        SeedMembership(context, candidate.Id, unitA.Id);
        SeedTask(context, employee.Id, oldManager.Id, unitA.Id, "Waiting for review", TaskStatusEnum.Submitted);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(candidate.Id, new UpdateUserDto
        {
            Role = "Manager",
            UnitId = unitA.Id,
            OldManagerId = oldManager.Id,
            OldManagerAction = "Transfer",
            OldManagerNewUnitId = unitB.Id
        }, admin.Id));

        var savedOldManager = await context.Users.SingleAsync(u => u.Id == oldManager.Id);
        var savedCandidate = await context.Users.SingleAsync(u => u.Id == candidate.Id);
        Assert.Equal("Manager", savedOldManager.Role);
        Assert.Equal(unitA.Id, savedOldManager.UnitId);
        Assert.Equal("User", savedCandidate.Role);
        Assert.Equal(unitA.Id, savedCandidate.UnitId);
    }

    [Fact]
    public async Task Update_RoleOnlyChange_DoesNotResetJoinedUnitAt()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "employee", "User", unit.Id);
        var joinedAt = DateTime.UtcNow.AddMonths(-6);
        user.JoinedUnitAt = joinedAt;
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserService(context);

        await service.Update(user.Id, new UpdateUserDto
        {
            Role = "Manager",
            UnitId = unit.Id
        }, admin.Id);

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("Manager", savedUser.Role);
        Assert.Equal(joinedAt, savedUser.JoinedUnitAt);
        Assert.Equal(1, savedUser.TokenVersion);
    }

    private static Unit SeedUnit(AppDbContext context, string name)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = name };
        context.Units.Add(unit);
        return unit;
    }

    private static User SeedUser(AppDbContext context, string username, string role, Guid? unitId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = username.ToUpperInvariant(),
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };
        context.Users.Add(user);
        return user;
    }

    private static void SeedTask(AppDbContext context, Guid userId, Guid createdBy, Guid unitId, string title, TaskStatusEnum status)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Status = status,
            UnitId = unitId
        };

        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId
        });
    }

    private static void SeedMembership(AppDbContext context, Guid userId, Guid unitId)
    {
        context.UserUnits.Add(new UserUnit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UnitId = unitId
        });
    }

    private static void SeedHistory(AppDbContext context, Guid userId, Guid? unitId, string role, DateTime from, DateTime? to)
    {
        context.UserWorkHistories.Add(new UserWorkHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UnitId = unitId,
            Role = role,
            EffectiveFrom = from,
            EffectiveTo = to,
            ChangeReason = "Test history"
        });
    }
}
