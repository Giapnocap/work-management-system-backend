using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class TaskServiceTests
{
    [Fact]
    public async Task Create_ByNonManager_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = SeedUser(context, role: "User");
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Create(new CreateTaskDto
        {
            Title = "Invalid task"
        }, user.Id));
    }

    [Fact]
    public async Task Create_ManagerWithoutUnit_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var manager = SeedUser(context, role: "Manager", unitId: null);
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Create(new CreateTaskDto
        {
            Title = "Missing unit"
        }, manager.Id));
    }

    [Fact]
    public async Task Create_WithDeadlineBeforeStartDate_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.Create(new CreateTaskDto
        {
            Title = "Invalid date range",
            StartDate = new DateTime(2026, 8, 10),
            DueDate = new DateTime(2026, 8, 9)
        }, manager.Id));

        Assert.Contains("Deadline", exception.Message);
        Assert.Empty(context.Tasks);
    }

    [Fact]
    public async Task Create_ManagerCannotAssignUserOutsideOwnUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "A");
        var unitB = SeedUnit(context, "B");
        var manager = SeedUser(context, role: "Manager", unitId: unitA.Id);
        var outsideUser = SeedUser(context, username: "outside", role: "User", unitId: unitB.Id);
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Create(new CreateTaskDto
        {
            Title = "Outside unit assignment",
            UserIds = new List<Guid> { outsideUser.Id }
        }, manager.Id));
    }

    [Fact]
    public async Task Create_WithNoDirectAssignee_SnapshotsCurrentUnitUsers()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        var secondEmployee = SeedUser(context, username: "second", role: "User", unitId: unit.Id);
        var notifications = new TestNotificationService();
        var transactions = new RecordingTransactionManager();
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context, notifications, transactions);

        var task = await service.Create(new CreateTaskDto
        {
            Title = "Build dashboard",
            RequiresReview = true
        }, manager.Id);

        var assignees = await context.TaskAssignees.OrderBy(a => a.UserId).ToListAsync();
        Assert.Equal(2, assignees.Count);
        Assert.All(assignees, assignee => Assert.Null(assignee.UnitId));
        Assert.Contains(assignees, assignee => assignee.UserId == employee.Id);
        Assert.Contains(assignees, assignee => assignee.UserId == secondEmployee.Id);
        Assert.Equal(TaskStatusEnum.NotStarted.ToString(), task.Status);
        Assert.Contains(notifications.Sent, n => n.UserId == employee.Id);
        Assert.Contains(notifications.Sent, n => n.UserId == secondEmployee.Id);
        Assert.Equal(1, transactions.ExecutionCount);
    }

    [Fact]
    public async Task Create_DepartmentSnapshot_DoesNotIncludeFutureUnitMembers()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        var task = await service.Create(new CreateTaskDto
        {
            Title = "Department snapshot"
        }, manager.Id);

        var futureEmployee = SeedUser(context, username: "future", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var access = new TaskAccessService(context);

        Assert.True(await access.CanAccessTask(task.Id, employee.Id));
        Assert.False(await access.CanAccessTask(task.Id, futureEmployee.Id));
    }

    [Fact]
    public async Task Create_WithNoAssignableUnitUsers_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Create(new CreateTaskDto
        {
            Title = "Department task"
        }, manager.Id));
        Assert.Equal(0, await context.Tasks.CountAsync());
    }

    [Fact]
    public async Task Create_CannotAssignManagerAsTaskAssignee()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var anotherManager = SeedUser(context, username: "manager2", role: "Manager", unitId: unit.Id);
        await context.SaveChangesAsync();

        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Create(new CreateTaskDto
        {
            Title = "Manager assignment",
            UserIds = new List<Guid> { anotherManager.Id }
        }, manager.Id));
    }

    [Fact]
    public async Task Update_DoesNotChangeExistingAssignees()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        var otherEmployee = SeedUser(context, username: "other", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        var task = await service.Create(new CreateTaskDto
        {
            Title = "Initial task",
            UserIds = new List<Guid> { employee.Id },
            RequiresReview = true
        }, manager.Id);

        await service.Update(task.Id, new UpdateTaskDto
        {
            Title = "Updated task",
            Description = "Updated description",
            DueDate = new DateTime(2026, 8, 1),
            Priority = "High",
            RequiresReview = false
        }, manager.Id);

        var savedTask = await context.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal("Updated task", savedTask.Title);
        Assert.Equal("Updated description", savedTask.Description);
        Assert.Equal("High", savedTask.Priority.ToString());
        Assert.False(savedTask.RequiresReview);

        var assignees = await context.TaskAssignees
            .Where(a => a.TaskId == task.Id)
            .ToListAsync();

        var assignee = Assert.Single(assignees);
        Assert.Equal(employee.Id, assignee.UserId);
        Assert.Null(assignee.UnitId);
        Assert.DoesNotContain(assignees, a => a.UserId == otherEmployee.Id);
    }

    [Fact]
    public async Task Update_WithDeadlineBeforeStartDate_DoesNotModifyTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        var task = await service.Create(new CreateTaskDto
        {
            Title = "Original task",
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(task.Id, new UpdateTaskDto
        {
            Title = "Should not be saved",
            StartDate = new DateTime(2026, 8, 10),
            DueDate = new DateTime(2026, 8, 9)
        }, manager.Id));

        var savedTask = await context.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal("Original task", savedTask.Title);
        Assert.Null(savedTask.StartDate);
        Assert.Null(savedTask.DueDate);
    }

    [Fact]
    public async Task Create_WithProjectFromAnotherUnit_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, role: "Manager", unitId: unitA.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unitA.Id);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Other unit project",
            UnitId = unitB.Id,
            CreatedBy = manager.Id
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Create(new CreateTaskDto
        {
            Title = "Invalid project task",
            ProjectId = project.Id,
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id));

        Assert.Empty(context.Tasks);
    }

    [Fact]
    public async Task Update_WithProjectFromAnotherUnit_DoesNotModifyTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, role: "Manager", unitId: unitA.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unitA.Id);
        var otherProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Other unit project",
            UnitId = unitB.Id,
            CreatedBy = manager.Id
        };
        context.Projects.Add(otherProject);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);
        var task = await service.Create(new CreateTaskDto
        {
            Title = "Scoped task",
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(
            task.Id,
            new UpdateTaskDto
            {
                Title = "Should not change",
                ProjectId = otherProject.Id
            },
            manager.Id));

        var savedTask = await context.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal("Scoped task", savedTask.Title);
        Assert.Null(savedTask.ProjectId);
    }

    [Fact]
    public async Task Update_CreatorMovedToAnotherUnit_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, role: "Manager", unitId: unitA.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unitA.Id);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);
        var task = await service.Create(new CreateTaskDto
        {
            Title = "Old unit task",
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id);
        manager.UnitId = unitB.Id;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(
            task.Id,
            new UpdateTaskDto { Title = "Cross-unit update" },
            manager.Id));

        Assert.Equal("Old unit task", (await context.Tasks.SingleAsync(t => t.Id == task.Id)).Title);
    }

    [Fact]
    public async Task Update_WhenProjectChanges_RecordsProjectHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Internal portal",
            UnitId = unit.Id,
            CreatedBy = manager.Id
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);
        var task = await service.Create(new CreateTaskDto
        {
            Title = "Scoped task",
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id);

        await service.Update(
            task.Id,
            new UpdateTaskDto
            {
                Title = task.Title,
                ProjectId = project.Id
            },
            manager.Id);

        var history = Assert.Single(await context.TaskHistories
            .Where(item => item.TaskId == task.Id && item.FieldName == "ProjectId")
            .ToListAsync());
        Assert.Equal(string.Empty, history.OldValue);
        Assert.Equal(project.Id.ToString(), history.NewValue);
    }

    [Fact]
    public async Task Update_ApprovedTaskCannotBeModified()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Archived project",
            UnitId = unit.Id,
            CreatedBy = manager.Id,
            IsArchived = true
        };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Completed task",
            Description = string.Empty,
            CreatedBy = manager.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UnitId = unit.Id,
            ProjectId = project.Id,
            Status = TaskStatusEnum.Approved,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            CompletedBy = manager.Id
        };
        context.Projects.Add(project);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(
            task.Id,
            new UpdateTaskDto
            {
                Title = "Changed completed task",
                ProjectId = project.Id
            },
            manager.Id));

        var savedTask = await context.Tasks.SingleAsync(item => item.Id == task.Id);
        Assert.Equal("Completed task", savedTask.Title);
        Assert.Equal(TaskStatusEnum.Approved, savedTask.Status);
        Assert.NotNull(savedTask.CompletedAt);
    }

    [Fact]
    public async Task ProjectStatusCounts_AreDerivedFromTaskStatuses()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var projectService = TestFactory.CreateProjectService(context);
        var taskService = TestFactory.CreateTaskService(context);

        var project = await projectService.CreateProject(new CreateProjectDto
        {
            Name = "Internal portal"
        }, manager.Id);

        var task = await taskService.Create(new CreateTaskDto
        {
            Title = "Build dashboard",
            ProjectId = project.Id,
            UserIds = new List<Guid> { employee.Id },
            RequiresReview = false
        }, manager.Id);

        var savedTask = await context.Tasks.SingleAsync(t => t.Id == task.Id);

        await TestFactory.CreateProgressService(context).Update(new CreateProgressDto
        {
            TaskId = savedTask.Id,
            Percent = 30,
            HoursSpent = 1
        }, employee.Id);

        var projects = await projectService.GetProjects(manager.Id);
        var statusCounts = projects.Single(p => p.Id == project.Id).StatusCounts;

        Assert.Equal(0, statusCounts.Single(c => c.Status == "NotStarted").Count);
        Assert.Equal(1, statusCounts.Single(c => c.Status == "InProgress").Count);
    }

    [Fact]
    public async Task Delete_CleanNotStartedTask_SoftDeletesAndRecordsHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = TestFactory.CreateTaskService(context, transactionManager: transactions);
        var task = await service.Create(new CreateTaskDto
        {
            Title = "Disposable draft",
            UserIds = new List<Guid> { employee.Id }
        }, manager.Id);

        var serializableExecutionsBeforeDelete = transactions.SerializableExecutionCount;
        await service.Delete(task.Id, manager.Id);

        var deletedTask = await context.Tasks
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == task.Id);
        Assert.True(deletedTask.IsDeleted);
        Assert.Equal(
            serializableExecutionsBeforeDelete + 1,
            transactions.SerializableExecutionCount);
        Assert.Contains(
            await context.TaskHistories.Where(item => item.TaskId == task.Id).ToListAsync(),
            history => history.FieldName == "IsDeleted" &&
                       history.NewValue == bool.TrueString);
    }

    [Fact]
    public async Task Delete_ApprovedTask_DoesNotModifyTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Completed task",
            CreatedBy = manager.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UnitId = unit.Id,
            Status = TaskStatusEnum.Approved,
            CompletedAt = DateTime.UtcNow,
            CompletedBy = manager.Id
        };
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Delete(task.Id, manager.Id));

        var savedTask = await context.Tasks.SingleAsync(item => item.Id == task.Id);
        Assert.False(savedTask.IsDeleted);
        Assert.Equal(TaskStatusEnum.Approved, savedTask.Status);
    }

    [Theory]
    [InlineData("progress")]
    [InlineData("upload")]
    [InlineData("comment")]
    [InlineData("subtask")]
    public async Task Delete_NotStartedTaskWithExecutionActivity_DoesNotModifyTask(string activity)
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Engineering");
        var manager = SeedUser(context, role: "Manager", unitId: unit.Id);
        var employee = SeedUser(context, username: "employee", role: "User", unitId: unit.Id);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task with activity",
            CreatedBy = manager.Id,
            CreatedAt = DateTime.UtcNow,
            UnitId = unit.Id,
            Status = TaskStatusEnum.NotStarted
        };
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id
        });
        await context.SaveChangesAsync();

        switch (activity)
        {
            case "progress":
                context.Progresses.Add(new Progress
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = employee.Id,
                    Percent = 10,
                    Status = ProgressStatusEnum.InProgress
                });
                break;
            case "upload":
                context.UploadFiles.Add(new UploadFile
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UploadedBy = employee.Id,
                    FileName = "evidence.pdf",
                    FilePath = "test/evidence.pdf",
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "comment":
                context.TaskComments.Add(new TaskComment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = employee.Id,
                    Content = "Started discussing the task."
                });
                break;
            case "subtask":
                context.SubTasks.Add(new SubTask
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    Title = "Execution step"
                });
                break;
            default:
                throw new InvalidOperationException($"Unsupported activity: {activity}");
        }

        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Delete(task.Id, manager.Id));

        var savedTask = await context.Tasks.SingleAsync(item => item.Id == task.Id);
        Assert.False(savedTask.IsDeleted);
    }

    private static Unit SeedUnit(AppDbContext context, string name)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = name };
        context.Units.Add(unit);
        return unit;
    }

    private static User SeedUser(AppDbContext context, string username = "user", string role = "User", Guid? unitId = null)
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
}
