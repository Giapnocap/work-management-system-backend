using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class TaskAccessSecurityTests
{
    [Fact]
    public async Task CanAccessTask_EnforcesCurrentRoleAssignmentAndUnitScope()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = new Unit { Id = Guid.NewGuid(), Name = "Unit A" };
        var unitB = new Unit { Id = Guid.NewGuid(), Name = "Unit B" };
        var currentManager = CreateUser("manager-a", SystemRoles.Manager, unitA.Id);
        var otherManager = CreateUser("manager-b", SystemRoles.Manager, unitB.Id);
        var formerCreator = CreateUser("former-manager", SystemRoles.User, unitB.Id);
        var assignee = CreateUser("assignee", SystemRoles.User, unitA.Id);
        var unassignedColleague = CreateUser("colleague", SystemRoles.User, unitA.Id);
        var admin = CreateUser("admin", SystemRoles.Admin, null);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Scoped task",
            Description = string.Empty,
            CreatedBy = formerCreator.Id,
            UnitId = unitA.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.AddRange(
            unitA,
            unitB,
            currentManager,
            otherManager,
            formerCreator,
            assignee,
            unassignedColleague,
            admin,
            task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = assignee.Id
        });
        await context.SaveChangesAsync();
        var service = new TaskAccessService(context);

        Assert.True(await service.CanAccessTask(task.Id, currentManager.Id));
        Assert.False(await service.CanAccessTask(task.Id, otherManager.Id));
        Assert.False(await service.CanAccessTask(task.Id, formerCreator.Id));
        Assert.True(await service.CanAccessTask(task.Id, assignee.Id));
        Assert.False(await service.CanAccessTask(task.Id, unassignedColleague.Id));
        Assert.True(await service.CanAccessTask(task.Id, admin.Id));
        Assert.False(await service.CanAccessTask(task.Id, assignee.Id, managementOnly: true));
    }

    [Fact]
    public async Task CanAccessUpload_InheritsTaskAuthorization()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Unit" };
        var manager = CreateUser("manager", SystemRoles.Manager, unit.Id);
        var assigned = CreateUser("assigned", SystemRoles.User, unit.Id);
        var outsider = CreateUser("outsider", SystemRoles.User, unit.Id);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Upload task",
            Description = string.Empty,
            CreatedBy = manager.Id,
            UnitId = unit.Id,
            CreatedAt = DateTime.UtcNow
        };
        var upload = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "evidence.pdf",
            StorageKey = "evidence.pdf",
            CreatedAt = DateTime.UtcNow,
            TaskId = task.Id,
            UploadedBy = assigned.Id
        };
        context.AddRange(unit, manager, assigned, outsider, task, upload);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = assigned.Id
        });
        await context.SaveChangesAsync();
        var service = new TaskAccessService(context);

        Assert.True(await service.CanAccessUpload(upload.Id, assigned.Id));
        Assert.False(await service.CanAccessUpload(upload.Id, outsider.Id));

        assigned.IsApproved = false;
        await context.SaveChangesAsync();
        Assert.False(await service.CanAccessUpload(upload.Id, assigned.Id));
    }

    private static User CreateUser(string username, string role, Guid? unitId)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = $"EMP{Guid.NewGuid():N}"[..12],
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };
}
