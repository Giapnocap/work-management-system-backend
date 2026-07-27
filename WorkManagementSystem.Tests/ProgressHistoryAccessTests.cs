using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class ProgressHistoryAccessTests
{
    [Fact]
    public async Task GetMyHistory_ManagerTransferredToAnotherUnit_OnlyReturnsCurrentUnitReports()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateProgressService(context);

        var result = await service.GetMyHistory(scenario.Manager.Id, 1, 20);
        var data = GetData(result);

        Assert.Equal(2, data.Count);
        Assert.Contains(data, progress => progress.Id == scenario.CurrentUnitProgress.Id);
        Assert.Contains(data, progress => progress.Id == scenario.OtherUserProgress.Id);
        Assert.DoesNotContain(data, progress => progress.Id == scenario.FormerUnitProgress.Id);
    }

    [Fact]
    public async Task GetMyHistory_ManagerWithoutUnit_ReturnsNoReports()
    {
        await using var context = TestFactory.CreateDbContext();
        await SeedScenario(context);
        var managerWithoutUnit = new User
        {
            Id = Guid.NewGuid(),
            Username = "manager_without_unit",
            FullName = "Manager Without Unit",
            EmployeeCode = "M002",
            PasswordHash = "hash",
            Role = "Manager",
            IsApproved = true
        };
        context.Users.Add(managerWithoutUnit);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        var result = await service.GetMyHistory(managerWithoutUnit.Id, 1, 20);

        Assert.Empty(GetData(result));
    }

    [Fact]
    public async Task GetMyHistory_UserOnlyReturnsOwnReports()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateProgressService(context);

        var result = await service.GetMyHistory(scenario.CurrentUnitUser.Id, 1, 20);
        var data = GetData(result);

        Assert.Single(data);
        Assert.Equal(scenario.CurrentUnitProgress.Id, data[0].Id);
    }

    [Fact]
    public async Task GetMyHistory_AdminReturnsAllReports()
    {
        await using var context = TestFactory.CreateDbContext();
        var scenario = await SeedScenario(context);
        var service = TestFactory.CreateProgressService(context);

        var result = await service.GetMyHistory(scenario.Admin.Id, 1, 20);
        var data = GetData(result);

        Assert.Equal(3, data.Count);
        Assert.Contains(data, progress => progress.Id == scenario.FormerUnitProgress.Id);
        Assert.Contains(data, progress => progress.Id == scenario.CurrentUnitProgress.Id);
        Assert.Contains(data, progress => progress.Id == scenario.OtherUserProgress.Id);
    }

    private static List<ProgressDto> GetData(object result)
    {
        var property = result.GetType().GetProperty("data");
        Assert.NotNull(property);
        return Assert.IsType<List<ProgressDto>>(property.GetValue(result));
    }

    private static async Task<HistoryScenario> SeedScenario(AppDbContext context)
    {
        var formerUnit = new Unit { Id = Guid.NewGuid(), Name = "Former Unit" };
        var currentUnit = new Unit { Id = Guid.NewGuid(), Name = "Current Unit" };
        var manager = CreateUser("history_manager", "M001", "Manager", currentUnit.Id);
        var formerUnitUser = CreateUser("former_user", "E001", "User", formerUnit.Id);
        var currentUnitUser = CreateUser("current_user", "E002", "User", currentUnit.Id);
        var otherCurrentUnitUser = CreateUser("other_current_user", "E003", "User", currentUnit.Id);
        var admin = CreateUser("history_admin", "A001", "Admin", null);

        var formerTask = CreateTask("Former task", manager.Id, formerUnit.Id);
        var currentTask = CreateTask("Current task", manager.Id, currentUnit.Id);

        var formerUnitProgress = CreateProgress(formerTask.Id, formerUnitUser.Id, DateTime.UtcNow.AddMinutes(-3));
        var currentUnitProgress = CreateProgress(currentTask.Id, currentUnitUser.Id, DateTime.UtcNow.AddMinutes(-2));
        var otherUserProgress = CreateProgress(currentTask.Id, otherCurrentUnitUser.Id, DateTime.UtcNow.AddMinutes(-1));

        context.Units.AddRange(formerUnit, currentUnit);
        context.Users.AddRange(manager, formerUnitUser, currentUnitUser, otherCurrentUnitUser, admin);
        context.Tasks.AddRange(formerTask, currentTask);
        context.Progresses.AddRange(formerUnitProgress, currentUnitProgress, otherUserProgress);
        await context.SaveChangesAsync();

        return new HistoryScenario(
            manager,
            currentUnitUser,
            admin,
            formerUnitProgress,
            currentUnitProgress,
            otherUserProgress);
    }

    private static User CreateUser(string username, string employeeCode, string role, Guid? unitId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = employeeCode,
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };
    }

    private static TaskItem CreateTask(string title, Guid managerId, Guid unitId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            CreatedBy = managerId,
            UnitId = unitId,
            RequiresReview = true,
            Status = TaskStatusEnum.Approved,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow
        };
    }

    private static Progress CreateProgress(Guid taskId, Guid userId, DateTime updatedAt)
    {
        return new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            Percent = 100,
            Status = ProgressStatusEnum.Approved,
            UpdatedAt = updatedAt
        };
    }

    private sealed record HistoryScenario(
        User Manager,
        User CurrentUnitUser,
        User Admin,
        Progress FormerUnitProgress,
        Progress CurrentUnitProgress,
        Progress OtherUserProgress);
}
