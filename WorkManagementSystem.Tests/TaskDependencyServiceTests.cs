using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public sealed class TaskDependencyServiceTests
{
    [Fact]
    public async Task AddAsync_WhenTaskDependsOnItself_IsRejected()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskA.Id,
            fixture.Manager.Id));

        Assert.Empty(context.TaskDependencies);
    }

    [Fact]
    public async Task AddAsync_WhenDirectCycleWouldBeCreated_IsRejected()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await service.AddAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
            fixture.TaskB.Id,
            fixture.TaskA.Id,
            fixture.Manager.Id));

        Assert.Single(context.TaskDependencies);
    }

    [Fact]
    public async Task AddAsync_WhenIndirectCycleWouldBeCreated_IsRejected()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await service.AddAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);
        await service.AddAsync(fixture.TaskB.Id, fixture.TaskC.Id, fixture.Manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
            fixture.TaskC.Id,
            fixture.TaskA.Id,
            fixture.Manager.Id));

        Assert.Equal(2, context.TaskDependencies.Count());
    }

    [Fact]
    public async Task AddAsync_WhenGraphRemainsDag_IsAccepted()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await service.AddAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);
        await service.AddAsync(fixture.TaskA.Id, fixture.TaskC.Id, fixture.Manager.Id);
        await service.AddAsync(fixture.TaskB.Id, fixture.TaskC.Id, fixture.Manager.Id);

        var graph = await service.GetGraphAsync(fixture.TaskA.Id, fixture.Manager.Id);

        Assert.Equal(3, context.TaskDependencies.Count());
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(3, graph.Edges.Count);
        Assert.True(graph.Nodes.Single(node => node.Id == fixture.TaskA.Id).IsBlocked);
    }

    [Fact]
    public async Task AddAsync_WhenDependencyAlreadyExists_IsRejected()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await service.AddAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskB.Id,
            fixture.Manager.Id));

        Assert.Single(context.TaskDependencies);
    }

    [Fact]
    public async Task Completion_WhenLastPredecessorCompletes_UnblocksDependentAndWritesHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var dependencyService = TestFactory.CreateTaskDependencyService(context);
        var workflowService = TestFactory.CreateTaskWorkflowService(context);
        var dtoBuilder = TestFactory.CreateTaskDtoBuilder(context);

        await dependencyService.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskB.Id,
            fixture.Manager.Id);

        var blockedTask = await dtoBuilder.BuildTaskDto(fixture.TaskA);
        Assert.True(blockedTask.IsBlocked);
        Assert.Equal(fixture.TaskB.Id, Assert.Single(blockedTask.BlockingTasks).Id);
        await Assert.ThrowsAsync<BusinessException>(() =>
            workflowService.EnsureDependenciesCompletedAsync(fixture.TaskA.Id));

        await workflowService.ApplyCompletionStateAsync(
            fixture.TaskB,
            fixture.Employee.Id);
        await context.SaveChangesAsync();

        var unblockedTask = await dtoBuilder.BuildTaskDto(fixture.TaskA);
        Assert.False(unblockedTask.IsBlocked);
        Assert.Empty(unblockedTask.BlockingTasks);
        Assert.Contains(context.TaskHistories, history =>
            history.TaskId == fixture.TaskA.Id &&
            history.FieldName == "DependencyUnblocked");
    }

    [Fact]
    public async Task AddAsync_WhenManagerBelongsToAnotherUnit_IsForbidden()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskB.Id,
            fixture.OtherManager.Id));

        Assert.Empty(context.TaskDependencies);
    }

    [Fact]
    public async Task AddAsync_WhenTasksBelongToDifferentProjects_IsRejected()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var unitId = fixture.TaskA.UnitId.GetValueOrDefault();
        Assert.NotEqual(Guid.Empty, unitId);
        var otherProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Other Dependency Project",
            UnitId = unitId,
            CreatedBy = fixture.Manager.Id,
            CreatedAt = DateTime.UtcNow
        };
        var otherProjectTask = CreateTask(
            "Other project task",
            fixture.Manager.Id,
            unitId,
            otherProject.Id);
        context.Projects.Add(otherProject);
        context.Tasks.Add(otherProjectTask);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskDependencyService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
            fixture.TaskA.Id,
            otherProjectTask.Id,
            fixture.Manager.Id));

        Assert.Empty(context.TaskDependencies);
    }

    [Fact]
    public async Task RemoveAsync_WhenLastBlockingDependencyIsRemoved_WritesAuditHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var service = TestFactory.CreateTaskDependencyService(context);

        await service.AddAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);
        await service.RemoveAsync(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id);

        Assert.Empty(context.TaskDependencies);
        var historyFields = await context.TaskHistories
            .Where(history => history.TaskId == fixture.TaskA.Id)
            .Select(history => history.FieldName)
            .ToListAsync();
        Assert.Contains("DependencyAdded", historyFields);
        Assert.Contains("DependencyRemoved", historyFields);
        Assert.Contains("DependencyUnblocked", historyFields);
    }

    [Fact]
    public async Task DeleteRule_WhenTaskParticipatesInDependency_RejectsBothEndpoints()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedAsync(context);
        var dependencyService = TestFactory.CreateTaskDependencyService(context);
        var taskRules = TestFactory.CreateTaskBusinessRuleService(context);

        await dependencyService.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskB.Id,
            fixture.Manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() =>
            taskRules.EnsureCanDelete(fixture.TaskA));
        await Assert.ThrowsAsync<BusinessException>(() =>
            taskRules.EnsureCanDelete(fixture.TaskB));
    }

    private static async Task<DependencyFixture> SeedAsync(AppDbContext context)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Dependency Unit" };
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other Dependency Unit" };
        var manager = CreateUser("dependency-manager", "MGR-D01", SystemRoles.Manager, unit.Id);
        var otherManager = CreateUser("other-dependency-manager", "MGR-D02", SystemRoles.Manager, otherUnit.Id);
        var employee = CreateUser("dependency-employee", "EMP-D01", SystemRoles.User, unit.Id);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Dependency Project",
            UnitId = unit.Id,
            CreatedBy = manager.Id,
            CreatedAt = DateTime.UtcNow
        };
        var taskA = CreateTask("Task A", manager.Id, unit.Id, project.Id);
        var taskB = CreateTask("Task B", manager.Id, unit.Id, project.Id);
        var taskC = CreateTask("Task C", manager.Id, unit.Id, project.Id);

        context.Units.AddRange(unit, otherUnit);
        context.Users.AddRange(manager, otherManager, employee);
        context.Projects.Add(project);
        context.Tasks.AddRange(taskA, taskB, taskC);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = taskB.Id,
            UserId = employee.Id
        });
        await context.SaveChangesAsync();

        return new DependencyFixture(manager, otherManager, employee, taskA, taskB, taskC);
    }

    private static User CreateUser(string username, string employeeCode, string role, Guid unitId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = employeeCode,
            PasswordHash = "not-used-by-this-test",
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = DateTime.UtcNow,
            IsApproved = true
        };
    }

    private static TaskItem CreateTask(
        string title,
        Guid managerId,
        Guid unitId,
        Guid projectId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedBy = managerId,
            CreatedAt = DateTime.UtcNow,
            UnitId = unitId,
            ProjectId = projectId,
            Status = TaskStatusEnum.NotStarted
        };
    }

    private sealed record DependencyFixture(
        User Manager,
        User OtherManager,
        User Employee,
        TaskItem TaskA,
        TaskItem TaskB,
        TaskItem TaskC);
}
