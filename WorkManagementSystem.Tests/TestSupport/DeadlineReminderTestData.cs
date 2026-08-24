using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests.TestSupport;

internal static class DeadlineReminderTestData
{
    public static async Task<DeadlineReminderFixture> SeedAsync(
        AppDbContext context,
        DateTime dueDateUtc,
        TaskStatusEnum status = TaskStatusEnum.InProgress,
        ReminderPolicyScope policyScope = ReminderPolicyScope.Global,
        bool policyActive = true,
        bool notifyAssignee = true,
        bool notifyManager = true,
        int beforeDueHours = 24,
        int overdueEscalationHours = 24)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = $"Deadline Unit {suffix}"
        };
        var manager = CreateUser(
            $"deadline-manager-{suffix}",
            $"MGR-{suffix}",
            "Deadline Manager",
            SystemRoles.Manager,
            unit.Id);
        var employee = CreateUser(
            $"deadline-user-{suffix}",
            $"EMP-{suffix}",
            "Deadline Employee",
            SystemRoles.User,
            unit.Id);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Deadline Project {suffix}",
            Description = string.Empty,
            UnitId = unit.Id,
            CreatedBy = manager.Id,
            CreatedAt = dueDateUtc.AddDays(-7)
        };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = $"Deadline task {suffix}",
            Description = string.Empty,
            CreatedBy = manager.Id,
            CreatedAt = dueDateUtc.AddDays(-2),
            StartDate = dueDateUtc.AddDays(-1),
            DueDate = dueDateUtc,
            Status = status,
            UnitId = unit.Id,
            ProjectId = project.Id
        };
        var policy = new ReminderPolicy
        {
            Id = Guid.NewGuid(),
            ScopeType = policyScope,
            UnitId = policyScope == ReminderPolicyScope.Unit ? unit.Id : null,
            ProjectId = policyScope == ReminderPolicyScope.Project ? project.Id : null,
            BeforeDueHours = beforeDueHours,
            OverdueEscalationHours = overdueEscalationHours,
            NotifyAssignee = notifyAssignee,
            NotifyManager = notifyManager,
            IsActive = policyActive,
            CreatedAtUtc = dueDateUtc.AddDays(-3),
            UpdatedAtUtc = dueDateUtc.AddDays(-3)
        };

        context.Units.Add(unit);
        context.Users.AddRange(manager, employee);
        context.Projects.Add(project);
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id
        });
        context.ReminderPolicies.Add(policy);
        await context.SaveChangesAsync();

        return new DeadlineReminderFixture(unit, manager, employee, project, task, policy);
    }

    public static async Task<User> AddManagerAsync(
        AppDbContext context,
        Guid unitId,
        string marker)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var manager = CreateUser(
            $"{marker}-{suffix}",
            $"MGR-{suffix}",
            marker,
            SystemRoles.Manager,
            unitId);
        context.Users.Add(manager);
        await context.SaveChangesAsync();
        return manager;
    }

    public static Task<List<ScheduledNotification>> GetEventsAsync(
        AppDbContext context,
        Guid taskId)
    {
        return context.ScheduledNotifications
            .AsNoTracking()
            .Where(notification => notification.TaskId == taskId)
            .OrderBy(notification => notification.Type)
            .ToListAsync();
    }

    private static User CreateUser(
        string username,
        string employeeCode,
        string fullName,
        string role,
        Guid unitId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = fullName,
            EmployeeCode = employeeCode,
            PasswordHash = "not-used-by-test",
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = DateTime.UtcNow.AddDays(-30),
            IsApproved = true
        };
    }
}

internal sealed record DeadlineReminderFixture(
    Unit Unit,
    User Manager,
    User Employee,
    Project Project,
    TaskItem Task,
    ReminderPolicy Policy);
