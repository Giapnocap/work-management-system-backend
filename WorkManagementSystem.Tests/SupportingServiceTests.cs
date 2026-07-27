using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;
using ProgressStatus = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class SupportingServiceTests
{
    [Fact]
    public async Task ManagerDashboard_DoesNotCountAssignedTasksFromAnotherUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var ownUnit = CreateUnit("Own unit");
        var otherUnit = CreateUnit("Other unit");
        var manager = CreateUser("manager", "Manager", ownUnit.Id);
        var employee = CreateUser("employee", "User", ownUnit.Id);
        var ownTask = CreateTask("Own task", manager.Id, ownUnit.Id, TaskStatus.NotStarted);
        var otherTask = CreateTask("Other task", manager.Id, otherUnit.Id, TaskStatus.Approved);

        context.AddRange(ownUnit, otherUnit, manager, employee, ownTask, otherTask);
        context.TaskAssignees.AddRange(
            CreateAssignee(ownTask.Id, employee.Id),
            CreateAssignee(otherTask.Id, employee.Id));
        context.Progresses.AddRange(
            new Progress
            {
                Id = Guid.NewGuid(),
                TaskId = ownTask.Id,
                UserId = employee.Id,
                Percent = 100,
                Status = ProgressStatus.Rejected
            },
            new Progress
            {
                Id = Guid.NewGuid(),
                TaskId = otherTask.Id,
                UserId = employee.Id,
                Percent = 100,
                Status = ProgressStatus.Rejected
            });
        await context.SaveChangesAsync();

        var result = await new DashboardService(context).GetManagerDashboard(manager.Id);

        Assert.Equal(1, result.TotalTasks);
        Assert.Equal(1, result.TaskPending);
        Assert.Equal(0, result.TaskApproved);
        Assert.Equal(1, result.RejectedReports);
        Assert.Equal(1, Assert.Single(result.MemberProgresses).TotalTasks);
    }

    [Fact]
    public async Task ExportService_RejectsEmployeeEvenWhenCalledOutsideController()
    {
        await using var context = TestFactory.CreateDbContext();
        var employee = CreateUser("employee", "User", null);
        context.Users.Add(employee);
        await context.SaveChangesAsync();

        var service = new ExportService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ExportTasksToExcel(employee.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ExportProgressToExcel(employee.Id));
    }

    [Fact]
    public async Task DashboardService_ObservesCancellationBeforeDatabaseWork()
    {
        await using var context = TestFactory.CreateDbContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DashboardService(context).GetDashboard(cancellation.Token));
    }

    [Fact]
    public async Task ChangePassword_WrongOldPasswordDoesNotMutateStoredHash()
    {
        await using var context = TestFactory.CreateDbContext();
        var employee = CreateUser("employee", "User", null);
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");
        context.Users.Add(employee);
        await context.SaveChangesAsync();
        var originalHash = employee.PasswordHash;

        var result = await new ChangePasswordService(context, TestFactory.CreateAuditService(context)).ChangePassword(employee.Id, new ChangePasswordDto
        {
            OldPassword = "WrongPassword",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });

        Assert.Equal("Mật khẩu cũ không đúng!", result);
        Assert.Equal(originalHash, employee.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password@123", employee.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_InvalidatesExistingSessions()
    {
        await using var context = TestFactory.CreateDbContext();
        var employee = CreateUser("employee", "User", null);
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");
        employee.TokenVersion = 4;
        context.Users.Add(employee);
        await context.SaveChangesAsync();

        var result = await new ChangePasswordService(context, TestFactory.CreateAuditService(context)).ChangePassword(employee.Id, new ChangePasswordDto
        {
            OldPassword = "Password@123",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });

        Assert.Equal("Đổi mật khẩu thành công!", result);
        Assert.Equal(5, employee.TokenVersion);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword@123", employee.PasswordHash));
    }

    [Fact]
    public async Task CommentService_NonOwnerEmployeeCannotDeleteAnotherUsersComment()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = CreateUnit("Engineering");
        var manager = CreateUser("manager", "Manager", unit.Id);
        var author = CreateUser("author", "User", unit.Id);
        var colleague = CreateUser("colleague", "User", unit.Id);
        var task = CreateTask("Shared task", manager.Id, unit.Id, TaskStatus.InProgress);

        context.AddRange(unit, manager, author, colleague, task);
        context.TaskAssignees.AddRange(
            CreateAssignee(task.Id, author.Id),
            CreateAssignee(task.Id, colleague.Id));
        await context.SaveChangesAsync();

        var notifications = new TestNotificationService();
        var service = new CommentService(
            TestFactory.Repo<TaskComment>(context),
            TestFactory.Repo<User>(context),
            TestFactory.Repo<TaskItem>(context),
            TestFactory.Repo<CommentReaction>(context),
            TestFactory.Repo<CommentSeen>(context),
            notifications,
            new TaskAccessService(context),
            TestFactory.CreateTaskWorkflowService(context),
            TestFactory.CreateMapper());

        var comment = await service.AddComment(new CreateCommentDto
        {
            TaskId = task.Id,
            Content = "  Work update  "
        }, author.Id);

        Assert.Equal("Work update", comment.Content);
        Assert.Single(await context.CommentSeens.ToListAsync());
        Assert.Contains(notifications.Sent, item => item.UserId == colleague.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Delete(comment.Id, colleague.Id));
        Assert.False((await context.TaskComments.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    private static Unit CreateUnit(string name)
        => new() { Id = Guid.NewGuid(), Name = name };

    private static User CreateUser(string username, string role, Guid? unitId)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = username,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}"[..12],
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId,
            IsApproved = true
        };

    private static TaskItem CreateTask(string title, Guid createdBy, Guid unitId, TaskStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedBy = createdBy,
            UnitId = unitId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

    private static TaskAssignee CreateAssignee(Guid taskId, Guid userId)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId
        };
}
