using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class TaskQueryServiceTests
{
    [Fact]
    public async Task Get_UserOnlySeesDirectAndUnclaimedDepartmentTasks()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateTaskQueryService(context);

        var result = await service.Get(string.Empty, 1, 20, null, scenario.Employee.Id);
        var tasks = result.Data;

        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, task => task.Id == scenario.DirectTask.Id);
        Assert.Contains(tasks, task => task.Id == scenario.DepartmentTask.Id);
        Assert.DoesNotContain(tasks, task => task.Id == scenario.ColleagueTask.Id);
        Assert.DoesNotContain(tasks, task => task.Id == scenario.OtherUnitTask.Id);
    }

    [Fact]
    public async Task Get_ManagerOnlySeesCurrentUnitTasks()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateTaskQueryService(context);

        var result = await service.Get(string.Empty, 1, 20, null, scenario.Manager.Id);
        var tasks = result.Data;

        Assert.Equal(3, tasks.Count);
        Assert.DoesNotContain(tasks, task => task.Id == scenario.OtherUnitTask.Id);
    }

    [Fact]
    public async Task Get_AdminSeesAllTasks()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateTaskQueryService(context);

        var result = await service.Get(string.Empty, 1, 20, null, scenario.Admin.Id);

        Assert.Equal(4, result.Data.Count);
    }

    private static async Task<QueryScenario> SeedScenario(AppDbContext context)
    {
        var currentUnit = new Unit { Id = Guid.NewGuid(), Name = "Current Unit" };
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other Unit" };
        var manager = CreateUser("query-manager", SystemRoles.Manager, currentUnit.Id);
        var employee = CreateUser("query-employee", SystemRoles.User, currentUnit.Id);
        var colleague = CreateUser("query-colleague", SystemRoles.User, currentUnit.Id);
        var outsideEmployee = CreateUser("query-outside", SystemRoles.User, otherUnit.Id);
        var admin = CreateUser("query-admin", SystemRoles.Admin, null);

        var directTask = CreateTask("Direct task", manager.Id, currentUnit.Id);
        var departmentTask = CreateTask("Department task", manager.Id, currentUnit.Id);
        var colleagueTask = CreateTask("Colleague task", manager.Id, currentUnit.Id);
        var otherUnitTask = CreateTask("Other unit task", manager.Id, otherUnit.Id);

        context.Units.AddRange(currentUnit, otherUnit);
        context.Users.AddRange(manager, employee, colleague, outsideEmployee, admin);
        context.Tasks.AddRange(directTask, departmentTask, colleagueTask, otherUnitTask);
        context.TaskAssignees.AddRange(
            CreateUserAssignee(directTask.Id, employee.Id),
            CreateUnitAssignee(departmentTask.Id, currentUnit.Id),
            CreateUserAssignee(colleagueTask.Id, colleague.Id),
            CreateUserAssignee(otherUnitTask.Id, outsideEmployee.Id));
        await context.SaveChangesAsync();

        return new QueryScenario(
            manager,
            employee,
            admin,
            directTask,
            departmentTask,
            colleagueTask,
            otherUnitTask);
    }

    private static User CreateUser(string username, string role, Guid? unitId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = $"E{Guid.NewGuid():N}"[..8],
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };
    }

    private static TaskItem CreateTask(string title, Guid createdBy, Guid unitId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            CreatedBy = createdBy,
            UnitId = unitId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TaskAssignee CreateUserAssignee(Guid taskId, Guid userId)
        => new() { Id = Guid.NewGuid(), TaskId = taskId, UserId = userId };

    private static TaskAssignee CreateUnitAssignee(Guid taskId, Guid unitId)
        => new() { Id = Guid.NewGuid(), TaskId = taskId, UnitId = unitId };

    private sealed record QueryScenario(
        User Manager,
        User Employee,
        User Admin,
        TaskItem DirectTask,
        TaskItem DepartmentTask,
        TaskItem ColleagueTask,
        TaskItem OtherUnitTask);
}
