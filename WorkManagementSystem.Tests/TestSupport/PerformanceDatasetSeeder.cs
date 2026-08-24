using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests.TestSupport;

public static class PerformanceDatasetSeeder
{
    public const int EmployeeCount = 30;
    public const int TaskCount = 300;

    public static async Task<TaskListPerformanceFixture> SeedTaskListAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var createdAt = new DateTime(2037, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = $"Performance SQL {suffix}"
        };
        var manager = CreateUser(
            $"performance-manager-{suffix}",
            $"PERF-MGR-{suffix}",
            SystemRoles.Manager,
            unit.Id,
            createdAt);
        var employees = Enumerable.Range(1, EmployeeCount)
            .Select(index => CreateUser(
                $"performance-user-{index}-{suffix}",
                $"PERF-{index}-{suffix}",
                SystemRoles.User,
                unit.Id,
                createdAt))
            .ToArray();
        var tasks = Enumerable.Range(1, TaskCount)
            .Select(index => new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Performance task {index:D3}",
                Description = $"Task list query budget fixture {index:D3}.",
                CreatedBy = manager.Id,
                CreatedAt = createdAt.AddMinutes(index),
                DueDate = createdAt.AddDays(index % 45 + 1),
                UnitId = unit.Id,
                Status = index % 4 == 0 ? TaskStatusEnum.InProgress : TaskStatusEnum.NotStarted,
                Priority = index % 3 == 0 ? TaskPriority.High : TaskPriority.Medium
            })
            .ToArray();

        context.Units.Add(unit);
        context.Users.Add(manager);
        context.Users.AddRange(employees);
        context.Tasks.AddRange(tasks);
        context.TaskAssignees.AddRange(tasks.Select((task, index) => new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employees[index % employees.Length].Id
        }));
        context.SubTasks.AddRange(tasks.Select((task, index) => new SubTask
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Title = $"Performance subtask {index + 1:D3}",
            CreatedAt = createdAt.AddMinutes(index)
        }));
        context.UploadFiles.AddRange(tasks
            .Where((_, index) => index % 5 == 0)
            .Select((task, index) => new UploadFile
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UploadedBy = manager.Id,
                FileName = $"performance-{index + 1:D3}.txt",
                StorageKey = $"performance/{suffix}/{index + 1:D3}.txt",
                CreatedAt = createdAt.AddMinutes(index)
            }));
        context.TaskDependencies.AddRange(tasks
            .Skip(1)
            .Where((_, index) => index % 10 == 0)
            .Select((task, index) => new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                DependsOnTaskId = tasks[index * 10].Id,
                CreatedAt = createdAt,
                CreatedByUserId = manager.Id
            }));

        await context.SaveChangesAsync(cancellationToken);
        return new TaskListPerformanceFixture(manager.Id, unit.Id, tasks.Length);
    }

    private static User CreateUser(
        string username,
        string employeeCode,
        string role,
        Guid unitId,
        DateTime joinedUnitAt)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = $"Performance {role}",
            EmployeeCode = employeeCode,
            PasswordHash = "not-used-by-this-test",
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = joinedUnitAt,
            IsApproved = true
        };
    }
}

public sealed record TaskListPerformanceFixture(
    Guid ManagerId,
    Guid UnitId,
    int TaskCount);
