using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class ProgressReviewServiceTests
{
    [Fact]
    public async Task Update_WhenCompletingReviewRequiredTaskWithoutFile_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 2
        }, user.Id));
    }

    [Fact]
    public async Task Update_ByManager_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, _, task) = await SeedAssignedTask(context, requiresReview: false);
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 30,
            HoursSpent = 1
        }, manager.Id));
    }

    [Fact]
    public async Task Update_ByAdmin_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, _, task) = await SeedAssignedTask(context, requiresReview: false);
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin_reporter",
            FullName = "Admin Reporter",
            EmployeeCode = "A002",
            PasswordHash = "hash",
            Role = "Admin",
            IsApproved = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 30,
            HoursSpent = 1
        }, admin.Id));
    }

    [Fact]
    public async Task Update_ByUnassignedUser_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, _, task) = await SeedAssignedTask(context, requiresReview: false);
        var outsider = new User
        {
            Id = Guid.NewGuid(),
            Username = "outsider",
            FullName = "Outsider",
            EmployeeCode = "E999",
            PasswordHash = "hash",
            Role = "User",
            UnitId = manager.UnitId,
            IsApproved = true
        };
        context.Users.Add(outsider);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 30,
            HoursSpent = 1
        }, outsider.Id));
    }

    [Fact]
    public async Task Update_PartialProgress_ClampsInvalidValues_AndSetsTaskInProgress()
    {
        var saveCounter = new SaveChangesCounterInterceptor();
        await using var context = TestFactory.CreateDbContext(saveCounter);
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: false);
        saveCounter.Reset();
        var transactions = new RecordingTransactionManager();
        var service = TestFactory.CreateProgressService(context, transactionManager: transactions);

        var result = await service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 50,
            HoursSpent = -3,
            Description = "Halfway"
        }, user.Id);

        var savedTask = await context.Tasks.FindAsync(task.Id);
        Assert.Equal(0, result.HoursSpent);
        Assert.Equal("InProgress", result.Status);
        Assert.Equal(TaskStatusEnum.InProgress, savedTask!.Status);
        Assert.Equal(1, transactions.ExecutionCount);
        Assert.Equal(1, saveCounter.Count);
    }

    [Fact]
    public async Task Update_CompleteTaskWithoutReview_ApprovesProgressAndCompletesTask()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: false);
        var service = TestFactory.CreateProgressService(context);

        var result = await service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 4
        }, user.Id);

        var savedTask = await context.Tasks.FindAsync(task.Id);
        Assert.Equal("Approved", result.Status);
        Assert.Equal(TaskStatusEnum.Approved, savedTask!.Status);
        Assert.Equal(4, savedTask.ActualHours);
        Assert.NotNull(savedTask.CompletedAt);
    }

    [Fact]
    public async Task Update_WhenTaskHasMultipleAssignees_CompletesOnlyAfterEveryAssigneeApproved()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, firstUser, task) = await SeedAssignedTask(context, requiresReview: false);
        var secondUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "second_employee",
            FullName = "Second Employee",
            EmployeeCode = "E002",
            PasswordHash = "hash",
            Role = "User",
            UnitId = manager.UnitId,
            IsApproved = true
        };
        context.Users.Add(secondUser);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = secondUser.Id
        });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 2
        }, firstUser.Id);

        var afterFirstReport = await context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskStatusEnum.InProgress, afterFirstReport!.Status);
        Assert.Null(afterFirstReport.CompletedAt);

        await service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 3
        }, secondUser.Id);

        var afterSecondReport = await context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskStatusEnum.Approved, afterSecondReport!.Status);
        Assert.Equal(5, afterSecondReport.ActualHours);
        Assert.NotNull(afterSecondReport.CompletedAt);
    }

    [Fact]
    public async Task Update_WhenTaskAlreadyApproved_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: false);
        task.Status = TaskStatusEnum.Approved;
        task.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 40,
            HoursSpent = 1
        }, user.Id));

        Assert.Equal(0, await context.Progresses.CountAsync());
    }

    [Fact]
    public async Task Update_WithFileAttachedToAnotherTask_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var otherTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Other task",
            Description = "",
            CreatedBy = manager.Id,
            UnitId = manager.UnitId,
            RequiresReview = true,
            Status = TaskStatusEnum.NotStarted,
            CreatedAt = DateTime.UtcNow
        };
        var file = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = "evidence.pdf",
            StorageKey = "evidence.pdf",
            CreatedAt = DateTime.UtcNow,
            UploadedBy = user.Id,
            TaskId = otherTask.Id
        };
        context.Tasks.Add(otherTask);
        context.UploadFiles.Add(file);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 2,
            FileId = file.Id
        }, user.Id));
    }

    [Fact]
    public async Task Update_PartialProgressKeepsTaskSubmitted_WhenAnotherReportIsPending()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, firstUser, task) = await SeedAssignedTask(context, requiresReview: true);
        var secondUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "second_employee",
            FullName = "Second Employee",
            EmployeeCode = "E002",
            PasswordHash = "hash",
            Role = "User",
            UnitId = manager.UnitId,
            IsApproved = true
        };
        context.Users.Add(secondUser);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = secondUser.Id
        });
        context.Progresses.Add(new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = firstUser.Id,
            Percent = 100,
            HoursSpent = 2,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        });
        task.Status = TaskStatusEnum.Submitted;
        await context.SaveChangesAsync();
        var service = TestFactory.CreateProgressService(context);

        await service.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 40,
            HoursSpent = 1
        }, secondUser.Id);

        var savedTask = await context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskStatusEnum.Submitted, savedTask!.Status);
    }

    [Fact]
    public async Task Review_ApproveSubmittedProgress_CompletesTaskAndAddsNotification()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 100,
            HoursSpent = 5,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        };
        context.Progresses.Add(progress);
        task.Status = TaskStatusEnum.Submitted;
        await context.SaveChangesAsync();
        var notifications = new TestNotificationService();
        var transactions = new RecordingTransactionManager();
        var service = TestFactory.CreateReviewService(context, notifications, transactions);

        await service.Review(new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true,
            Comment = "OK"
        }, manager.Id);

        var savedTask = await context.Tasks.FindAsync(task.Id);
        var savedProgress = await context.Progresses.FindAsync(progress.Id);
        Assert.Equal(ProgressStatusEnum.Approved, savedProgress!.Status);
        Assert.Equal(TaskStatusEnum.Approved, savedTask!.Status);
        Assert.Equal(5, savedTask.ActualHours);
        Assert.Contains(notifications.Sent, n => n.UserId == user.Id && n.Message.Contains("phe duyet"));
        Assert.Equal(1, transactions.ExecutionCount);
        Assert.Equal(1, transactions.SerializableExecutionCount);
    }

    [Fact]
    public async Task Review_ApproveKeepsTaskSubmitted_WhenAnotherReportIsStillPending()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, firstUser, task) = await SeedAssignedTask(context, requiresReview: true);
        var secondUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "second_employee",
            FullName = "Second Employee",
            EmployeeCode = "E002",
            PasswordHash = "hash",
            Role = "User",
            UnitId = manager.UnitId,
            IsApproved = true
        };
        var firstProgress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = firstUser.Id,
            Percent = 100,
            HoursSpent = 2,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        };
        var secondProgress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = secondUser.Id,
            Percent = 100,
            HoursSpent = 3,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(secondUser);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = secondUser.Id
        });
        context.Progresses.AddRange(firstProgress, secondProgress);
        task.Status = TaskStatusEnum.Submitted;
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReviewService(context);

        await service.Review(new ReviewDto
        {
            ProgressId = firstProgress.Id,
            Approve = true
        }, manager.Id);

        var savedTask = await context.Tasks.FindAsync(task.Id);
        var savedSecondProgress = await context.Progresses.FindAsync(secondProgress.Id);
        Assert.Equal(TaskStatusEnum.Submitted, savedTask!.Status);
        Assert.Equal(ProgressStatusEnum.Submitted, savedSecondProgress!.Status);
    }

    [Fact]
    public async Task Review_ByAdmin_ThrowsForbiddenException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "Admin",
            EmployeeCode = "A001",
            PasswordHash = "hash",
            Role = "Admin",
            IsApproved = true
        };
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 100,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(admin);
        context.Progresses.Add(progress);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReviewService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Review(new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true
        }, admin.Id));
    }

    [Fact]
    public async Task Review_AlreadyReviewedProgress_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 100,
            Status = ProgressStatusEnum.Submitted
        };
        context.Progresses.Add(progress);
        context.Reviews.Add(new ReportReview
        {
            Id = Guid.NewGuid(),
            ProgressId = progress.Id,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow,
            ReviewerId = manager.Id
        });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReviewService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.Review(new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true
        }, manager.Id));
    }

    [Fact]
    public async Task Review_RejectThenCorrectedResubmission_CompletesThroughSupportedWorkflow()
    {
        await using var context = TestFactory.CreateDbContext();
        var (manager, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var progressService = TestFactory.CreateProgressService(context);
        var reviewService = TestFactory.CreateReviewService(context);
        var firstEvidence = await AddEvidence(context, task.Id, user.Id, "first-evidence.pdf");

        var firstSubmission = await progressService.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 3,
            FileId = firstEvidence.Id
        }, user.Id);

        Assert.Equal(ProgressStatusEnum.Submitted.ToString(), firstSubmission.Status);
        Assert.Equal(
            TaskStatusEnum.Submitted,
            (await context.Tasks.AsNoTracking().SingleAsync(item => item.Id == task.Id)).Status);

        await Assert.ThrowsAsync<BusinessException>(() => progressService.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 2,
            FileId = firstEvidence.Id
        }, user.Id));

        await reviewService.Review(new ReviewDto
        {
            ProgressId = firstSubmission.Id,
            Approve = false,
            Comment = "Can bo sung minh chung."
        }, manager.Id);

        var rejectedProgress = await context.Progresses
            .AsNoTracking()
            .SingleAsync(item => item.Id == firstSubmission.Id);
        var taskAfterRejection = await context.Tasks
            .AsNoTracking()
            .SingleAsync(item => item.Id == task.Id);

        Assert.Equal(ProgressStatusEnum.Rejected, rejectedProgress.Status);
        Assert.Equal(TaskStatusEnum.InProgress, taskAfterRejection.Status);
        Assert.Null(taskAfterRejection.CompletedAt);
        Assert.Null(taskAfterRejection.CompletedBy);

        var correctedEvidence = await AddEvidence(
            context,
            task.Id,
            user.Id,
            "corrected-evidence.pdf");
        var correctedSubmission = await progressService.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 100,
            HoursSpent = 2,
            FileId = correctedEvidence.Id
        }, user.Id);

        await reviewService.Review(new ReviewDto
        {
            ProgressId = correctedSubmission.Id,
            Approve = true,
            Comment = "Dat yeu cau."
        }, manager.Id);

        var completedTask = await context.Tasks
            .AsNoTracking()
            .SingleAsync(item => item.Id == task.Id);

        Assert.Equal(TaskStatusEnum.Approved, completedTask.Status);
        Assert.NotNull(completedTask.CompletedAt);
        Assert.Equal(user.Id, completedTask.CompletedBy);
        Assert.Equal(2, await context.Reviews.CountAsync());

        await Assert.ThrowsAsync<BusinessException>(() => progressService.Update(new CreateProgressDto
        {
            TaskId = task.Id,
            Percent = 10,
            HoursSpent = 1
        }, user.Id));
    }

    [Fact]
    public async Task Review_ByManagerFromAnotherUnit_DoesNotChangeSubmittedReport()
    {
        await using var context = TestFactory.CreateDbContext();
        var (_, user, task) = await SeedAssignedTask(context, requiresReview: true);
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Operations" };
        var otherManager = new User
        {
            Id = Guid.NewGuid(),
            Username = "operations_manager",
            FullName = "Operations Manager",
            EmployeeCode = "M002",
            PasswordHash = "hash",
            Role = "Manager",
            UnitId = otherUnit.Id,
            IsApproved = true
        };
        var progress = new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 100,
            Status = ProgressStatusEnum.Submitted,
            UpdatedAt = DateTime.UtcNow
        };
        task.Status = TaskStatusEnum.Submitted;
        context.Units.Add(otherUnit);
        context.Users.Add(otherManager);
        context.Progresses.Add(progress);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateReviewService(context);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.Review(new ReviewDto
        {
            ProgressId = progress.Id,
            Approve = true
        }, otherManager.Id));

        Assert.Equal(
            ProgressStatusEnum.Submitted,
            (await context.Progresses.AsNoTracking().SingleAsync(item => item.Id == progress.Id)).Status);
        Assert.Equal(
            TaskStatusEnum.Submitted,
            (await context.Tasks.AsNoTracking().SingleAsync(item => item.Id == task.Id)).Status);
        Assert.Empty(await context.Reviews.AsNoTracking().ToListAsync());
    }

    private static async Task<UploadFile> AddEvidence(
        AppDbContext context,
        Guid taskId,
        Guid uploadedBy,
        string fileName)
    {
        var file = new UploadFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            StorageKey = $"{Guid.NewGuid():N}.pdf",
            CreatedAt = DateTime.UtcNow,
            TaskId = taskId,
            UploadedBy = uploadedBy
        };
        context.UploadFiles.Add(file);
        await context.SaveChangesAsync();
        return file;
    }

    private static async Task<(User Manager, User User, TaskItem Task)> SeedAssignedTask(AppDbContext context, bool requiresReview)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Engineering" };
        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "manager",
            FullName = "Manager",
            EmployeeCode = "M001",
            PasswordHash = "hash",
            Role = "Manager",
            UnitId = unit.Id,
            IsApproved = true
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "employee",
            FullName = "Employee",
            EmployeeCode = "E001",
            PasswordHash = "hash",
            Role = "User",
            UnitId = unit.Id,
            IsApproved = true
        };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task",
            Description = "",
            CreatedBy = manager.Id,
            UnitId = unit.Id,
            RequiresReview = requiresReview,
            Status = TaskStatusEnum.NotStarted,
            CreatedAt = DateTime.UtcNow
        };
        context.Units.Add(unit);
        context.Users.AddRange(manager, user);
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id
        });
        await context.SaveChangesAsync();
        return (manager, user, task);
    }
}
