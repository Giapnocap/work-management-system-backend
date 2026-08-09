using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class UnitServiceTests
{
    [Fact]
    public async Task GetVisibleUsers_UserCannotReadAnotherUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var employee = SeedUser(context, "unit-viewer", "User", unitA.Id);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetVisibleUsers(unitB.Id, employee.Id));
    }

    [Fact]
    public async Task AddMemberForRequester_NonAdminIsForbidden()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, "unit-manager", "Manager", unit.Id);
        var employee = SeedUser(context, "new-member", "User", null);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AddMemberForRequester(unit.Id, employee.Id, manager.Id));

        Assert.Null((await context.Users.SingleAsync(user => user.Id == employee.Id)).UnitId);
    }

    [Fact]
    public async Task AddMember_WithUnknownUnit_ThrowsNotFoundException()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = SeedUser(context, "employee", "User", null);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.AddMember(Guid.NewGuid(), user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Null(savedUser.UnitId);
        Assert.Empty(await context.UserUnits.ToListAsync());
    }

    [Fact]
    public async Task AddMember_WhenUserBelongsToOtherUnit_ReplacesMembershipAndRecordsHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var joinedAt = DateTime.UtcNow.AddDays(-7);
        var user = SeedUser(context, "employee", "User", unitA.Id, joinedAt);
        SeedMembership(context, user.Id, unitA.Id);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = CreateService(context, transactions);

        await service.AddMember(unitB.Id, user.Id);

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unitB.Id, savedUser.UnitId);

        var mapping = Assert.Single(await context.UserUnits
            .Where(uu => uu.UserId == user.Id)
            .ToListAsync());
        Assert.Equal(unitB.Id, mapping.UnitId);

        var histories = await context.UserWorkHistories
            .Where(h => h.UserId == user.Id)
            .OrderBy(h => h.EffectiveFrom)
            .ToListAsync();

        Assert.Equal(2, histories.Count);
        Assert.Equal(unitA.Id, histories[0].UnitId);
        Assert.NotNull(histories[0].EffectiveTo);
        Assert.Equal(unitB.Id, histories[1].UnitId);
        Assert.Null(histories[1].EffectiveTo);
        Assert.Equal(1, transactions.ExecutionCount);
    }

    [Fact]
    public async Task AddMember_WhenUserHasPendingTaskInOldUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager", "Manager", unitA.Id);
        var user = SeedUser(context, "employee", "User", unitA.Id);
        SeedMembership(context, user.Id, unitA.Id);
        SeedTask(context, user.Id, manager.Id, unitA.Id, "Pending transfer task", TaskStatusEnum.InProgress);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddMember(unitB.Id, user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unitA.Id, savedUser.UnitId);
        var mapping = Assert.Single(await context.UserUnits.Where(uu => uu.UserId == user.Id).ToListAsync());
        Assert.Equal(unitA.Id, mapping.UnitId);
    }

    [Fact]
    public async Task Delete_WhenDirectMemberExistsWithoutMapping_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var user = SeedUser(context, "employee", "User", unit.Id);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Delete(unit.Id));

        var savedUnit = await context.Units.SingleAsync(u => u.Id == unit.Id);
        Assert.False(savedUnit.IsDeleted);
        Assert.Equal(unit.Id, (await context.Users.SingleAsync(u => u.Id == user.Id)).UnitId);
    }

    [Fact]
    public async Task RemoveMember_WhenUserDoesNotBelongToUnit_ThrowsNotFoundException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var user = SeedUser(context, "employee", "User", unitB.Id);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveMember(unitA.Id, user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unitB.Id, savedUser.UnitId);
        Assert.False(await context.UserUnits.AnyAsync(uu => uu.UserId == user.Id && uu.UnitId == unitA.Id));
    }

    [Fact]
    public async Task RemoveMember_WhenUserBelongsToUnit_ClearsDirectUnitAndMapping()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var user = SeedUser(context, "employee", "User", unit.Id);
        SeedMembership(context, user.Id, unit.Id);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.RemoveMember(unit.Id, user.Id);

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Null(savedUser.UnitId);
        Assert.False(await context.UserUnits.AnyAsync(uu => uu.UserId == user.Id));

        var histories = await context.UserWorkHistories
            .Where(h => h.UserId == user.Id)
            .OrderBy(h => h.EffectiveFrom)
            .ToListAsync();

        Assert.Equal(2, histories.Count);
        Assert.Equal(unit.Id, histories[0].UnitId);
        Assert.NotNull(histories[0].EffectiveTo);
        Assert.Null(histories[1].UnitId);
        Assert.Null(histories[1].EffectiveTo);
    }

    [Fact]
    public async Task RemoveMember_WhenUserHasPendingTaskInUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var user = SeedUser(context, "employee", "User", unit.Id);
        SeedMembership(context, user.Id, unit.Id);
        SeedTask(context, user.Id, manager.Id, unit.Id, "Pending task", TaskStatusEnum.InProgress);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.RemoveMember(unit.Id, user.Id));

        var savedUser = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(unit.Id, savedUser.UnitId);
        Assert.True(await context.UserUnits.AnyAsync(uu => uu.UserId == user.Id && uu.UnitId == unit.Id));
    }

    [Fact]
    public async Task RemoveMember_WhenManagerOwnsPendingUnitWork_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var employee = SeedUser(context, "employee", "User", unit.Id);
        SeedMembership(context, manager.Id, unit.Id);
        SeedTask(context, employee.Id, manager.Id, unit.Id, "Pending managed task", TaskStatusEnum.Submitted);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.RemoveMember(unit.Id, manager.Id));

        var savedManager = await context.Users.SingleAsync(u => u.Id == manager.Id);
        Assert.Equal("Manager", savedManager.Role);
        Assert.Equal(unit.Id, savedManager.UnitId);
        Assert.True(await context.UserUnits.AnyAsync(uu => uu.UserId == manager.Id && uu.UnitId == unit.Id));
    }

    private static UnitService CreateService(
        AppDbContext context,
        ITransactionManager? transactionManager = null)
    {
        return new UnitService(
            TestFactory.Repo<Unit>(context),
            TestFactory.Repo<UserUnit>(context),
            TestFactory.Repo<User>(context),
            TestFactory.CreateStaffMovementService(context),
            TestFactory.CreateMapper(),
            transactionManager ?? new EfTransactionManager(context),
            TestFactory.CreateAuditService(context),
            context);
    }

    private static Unit SeedUnit(AppDbContext context, string name)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = name };
        context.Units.Add(unit);
        return unit;
    }

    private static User SeedUser(AppDbContext context, string username, string role, Guid? unitId, DateTime? joinedUnitAt = null)
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
            JoinedUnitAt = joinedUnitAt ?? DateTime.UtcNow,
            IsApproved = true
        };
        context.Users.Add(user);
        return user;
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
}
