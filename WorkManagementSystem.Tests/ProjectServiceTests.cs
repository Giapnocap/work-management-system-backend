using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task CreateProject_ManagerWithoutUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var manager = SeedUser(context, "manager", null);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProjectService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.CreateProject(
            new CreateProjectDto { Name = "Invalid project" },
            manager.Id));

        Assert.Empty(context.Projects);
    }

    [Fact]
    public async Task UpdateProject_CannotChangeUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager", unitA.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProjectService(context);
        var project = await service.CreateProject(
            new CreateProjectDto { Name = "Internal portal" },
            manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProject(
            project.Id,
            new CreateProjectDto
            {
                Name = "Internal portal",
                UnitId = unitB.Id
            },
            manager.Id));

        Assert.Equal(unitA.Id, (await context.Projects.SingleAsync()).UnitId);
    }

    [Fact]
    public async Task ArchiveProject_WithActiveTask_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, "manager", unit.Id);
        var project = SeedProject(context, "Active project", unit.Id, manager.Id);
        context.Tasks.Add(SeedTask(project.Id, unit.Id, manager.Id, TaskStatusEnum.Submitted));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProjectService(context);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ArchiveProject(project.Id, manager.Id));

        Assert.False((await context.Projects.SingleAsync()).IsArchived);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ArchiveProject_WhenAllTasksApproved_ArchivesProject()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, "manager", unit.Id);
        var project = SeedProject(context, "Completed project", unit.Id, manager.Id);
        context.Tasks.Add(SeedTask(project.Id, unit.Id, manager.Id, TaskStatusEnum.Approved));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProjectService(context);

        await service.ArchiveProject(project.Id, manager.Id);

        var savedProject = await context.Projects.IgnoreQueryFilters().SingleAsync();
        Assert.True(savedProject.IsArchived);
        var audit = await context.AuditLogs.SingleAsync();
        Assert.Equal("Archived", audit.Action);
        Assert.Equal(manager.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task UpdateProject_CreatorMovedToAnotherUnit_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager", unitA.Id);
        var project = SeedProject(context, "Old unit project", unitA.Id, manager.Id);
        await context.SaveChangesAsync();
        manager.UnitId = unitB.Id;
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProjectService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateProject(
            project.Id,
            new CreateProjectDto { Name = "Should not change" },
            manager.Id));

        Assert.Equal("Old unit project", (await context.Projects.SingleAsync()).Name);
    }

    private static Unit SeedUnit(AppDbContext context, string name)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = name };
        context.Units.Add(unit);
        return unit;
    }

    private static User SeedUser(AppDbContext context, string username, Guid? unitId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = username.ToUpperInvariant(),
            PasswordHash = "hash",
            Role = "Manager",
            UnitId = unitId,
            IsApproved = true
        };
        context.Users.Add(user);
        return user;
    }

    private static Project SeedProject(
        AppDbContext context,
        string name,
        Guid unitId,
        Guid createdBy)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            UnitId = unitId,
            CreatedBy = createdBy
        };
        context.Projects.Add(project);
        return project;
    }

    private static TaskItem SeedTask(
        Guid projectId,
        Guid unitId,
        Guid createdBy,
        TaskStatusEnum status)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Project task",
            Description = string.Empty,
            ProjectId = projectId,
            UnitId = unitId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Status = status
        };
    }
}
