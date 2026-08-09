using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public class UserKpiWorkHistoryTests
{
    private static readonly DateTime PeriodStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodMiddle = new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task GetPerformance_WithUnitTransferInsidePeriod_MergesSegmentsAndCountsHistoricalTasks()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager", "Manager", unitA.Id);
        var user = SeedUser(context, "employee", "User", unitB.Id, joinedUnitAt: PeriodMiddle);
        var period = SeedPeriod(context);
        SeedHistory(context, user.Id, unitA.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, user.Id, unitB.Id, "User", PeriodMiddle, null);
        SeedApprovedTask(context, user.Id, manager.Id, unitA.Id, "Task A", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
        SeedApprovedTask(context, user.Id, manager.Id, unitB.Id, "Task B", new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal("Mixed", result.Role);
        Assert.Equal(unitB.Id, result.UnitId);
        Assert.True(result.IsPartialPeriod);
        Assert.Equal(2, result.TotalTasks);
        Assert.Equal(2, result.CompletedOnTime);
        Assert.Contains("nhieu giai doan", result.PeriodNote);
    }

    [Fact]
    public async Task GetPerformance_WithRoleChangeInsidePeriod_ReturnsMixedManagerKpi()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var user = SeedUser(context, "promoted", "Manager", unit.Id, joinedUnitAt: PeriodMiddle);
        var period = SeedPeriod(context);
        SeedHistory(context, user.Id, unit.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, user.Id, unit.Id, "Manager", PeriodMiddle, null);
        SeedApprovedTask(context, user.Id, user.Id, unit.Id, "Personal task", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal("Mixed", result.Role);
        Assert.True(result.IsManagerKpi);
        Assert.True(result.IsPartialPeriod);
        Assert.Equal(1, result.TotalTasks);
        Assert.Contains("nhieu giai doan", result.PeriodNote);
    }

    [Fact]
    public async Task GetPerformancesAsync_WithUnitTransfer_MatchesSingleUserCalculation()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager", "Manager", unitA.Id);
        var user = SeedUser(context, "batch-user", "User", unitB.Id, joinedUnitAt: PeriodMiddle);
        var period = SeedPeriod(context);
        SeedHistory(context, user.Id, unitA.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, user.Id, unitB.Id, "User", PeriodMiddle, null);
        SeedApprovedTask(context, user.Id, manager.Id, unitA.Id, "Task A", PeriodStart.AddDays(5));
        SeedApprovedTask(context, user.Id, manager.Id, unitB.Id, "Task B", PeriodMiddle.AddDays(5));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserPerformanceService(context);

        var single = await service.GetPerformanceAsync(user.Id, period.Id);
        var batch = Assert.Single(await service.GetPerformancesAsync(new[] { user.Id }, period.Id));

        Assert.Equal(single.Score, batch.Score);
        Assert.Equal(single.TotalTasks, batch.TotalTasks);
        Assert.Equal(single.CompletedOnTime, batch.CompletedOnTime);
        Assert.Equal(single.BonusPoints, batch.BonusPoints);
        Assert.Equal(single.Role, batch.Role);
        Assert.Equal(single.UnitId, batch.UnitId);
        Assert.Equal(single.EffectiveFrom, batch.EffectiveFrom);
        Assert.Equal(single.EffectiveTo, batch.EffectiveTo);
    }

    [Fact]
    public async Task GetPerformance_WhenPeriodLocked_UsesSnapshotInsteadOfLiveData()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var user = SeedUser(context, "locked-user", "User", unit.Id);
        var period = SeedPeriod(context, status: "Locked");
        SeedApprovedTask(context, user.Id, user.Id, unit.Id, "Live task", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
        context.KpiResults.Add(new KpiResult
        {
            Id = Guid.NewGuid(),
            PeriodId = period.Id,
            UserId = user.Id,
            UnitId = unit.Id,
            Role = "User",
            EffectiveFrom = PeriodStart,
            EffectiveTo = PeriodEnd,
            Score = 72,
            Level = "Snapshot",
            TotalTasks = 5,
            CompletedOnTime = 3,
            LockedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.True(result.IsLocked);
        Assert.Equal(72, result.Score);
        Assert.Equal("Snapshot", result.Level);
        Assert.Equal(5, result.TotalTasks);
        Assert.Contains("da chot", result.PeriodNote);
    }

    [Fact]
    public async Task GetUnitPerformance_IncludesEmployeeWhoMovedOutDuringPeriod()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager-a", "Manager", unitA.Id);
        var user = SeedUser(context, "moved-user", "User", unitB.Id, joinedUnitAt: PeriodMiddle);
        var period = SeedPeriod(context);
        SeedHistory(context, user.Id, unitA.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, user.Id, unitB.Id, "User", PeriodMiddle, null);
        SeedApprovedTask(context, user.Id, manager.Id, unitA.Id, "Old unit task", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetUnitPerformanceAsync(manager.Id, period.Id);

        var employeeKpi = Assert.Single(result, r => r.UserId == user.Id);
        Assert.Equal(unitA.Id, employeeKpi.UnitId);
        Assert.True(employeeKpi.IsPartialPeriod);
        Assert.Equal(1, employeeKpi.TotalTasks);
    }

    [Fact]
    public async Task GetUnitPerformance_IncludesEmployeePromotedAfterHistoricalUserSegment()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var manager = SeedUser(context, "manager-a", "Manager", unit.Id);
        var promotedUser = SeedUser(context, "promoted-user", "Manager", unit.Id, joinedUnitAt: PeriodMiddle);
        var period = SeedPeriod(context);
        SeedHistory(context, promotedUser.Id, unit.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, promotedUser.Id, unit.Id, "Manager", PeriodMiddle, null);
        SeedApprovedTask(context, promotedUser.Id, manager.Id, unit.Id, "Before promotion task", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetUnitPerformanceAsync(manager.Id, period.Id);

        var employeeKpi = Assert.Single(result, r => r.UserId == promotedUser.Id);
        Assert.Equal("User", employeeKpi.Role);
        Assert.True(employeeKpi.IsPartialPeriod);
        Assert.Equal(1, employeeKpi.TotalTasks);
    }

    [Fact]
    public async Task CanViewPerformance_UsesHistoricalUnitInsteadOfDeletedUsersCurrentUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var historicalUnit = SeedUnit(context, "Historical Unit");
        var currentUnit = SeedUnit(context, "Current Unit");
        var historicalManager = SeedUser(context, "historical-manager", "Manager", historicalUnit.Id);
        var currentManager = SeedUser(context, "current-manager", "Manager", currentUnit.Id);
        var formerEmployee = SeedUser(context, "former-employee", "User", currentUnit.Id);
        formerEmployee.IsDeleted = true;
        var period = SeedPeriod(context);
        SeedHistory(
            context,
            formerEmployee.Id,
            historicalUnit.Id,
            "User",
            PeriodStart,
            PeriodEnd);
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var historicalManagerCanView = await service.CanViewPerformanceAsync(
            historicalManager.Id,
            formerEmployee.Id,
            period.Id);
        var currentManagerCanView = await service.CanViewPerformanceAsync(
            currentManager.Id,
            formerEmployee.Id,
            period.Id);

        Assert.True(historicalManagerCanView);
        Assert.False(currentManagerCanView);
    }

    [Fact]
    public async Task CanViewPerformance_WhenPeriodLocked_UsesKpiResultUnitSnapshot()
    {
        await using var context = TestFactory.CreateDbContext();
        var historicalUnit = SeedUnit(context, "Historical Unit");
        var currentUnit = SeedUnit(context, "Current Unit");
        var manager = SeedUser(context, "historical-manager", "Manager", historicalUnit.Id);
        var formerEmployee = SeedUser(context, "former-employee", "User", currentUnit.Id);
        formerEmployee.IsDeleted = true;
        var period = SeedPeriod(context, status: "Locked");
        context.KpiResults.Add(new KpiResult
        {
            Id = Guid.NewGuid(),
            PeriodId = period.Id,
            UserId = formerEmployee.Id,
            UnitId = historicalUnit.Id,
            Role = "User",
            FullNameSnapshot = formerEmployee.FullName,
            EmployeeCodeSnapshot = formerEmployee.EmployeeCode,
            UnitNameSnapshot = historicalUnit.Name,
            EffectiveFrom = PeriodStart,
            EffectiveTo = PeriodEnd,
            Score = 100,
            Level = "Moi/Thu viec",
            LockedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var canView = await service.CanViewPerformanceAsync(
            manager.Id,
            formerEmployee.Id,
            period.Id);

        Assert.True(canView);
    }

    [Fact]
    public async Task GetPerformance_WithDepartmentTaskSnapshot_KeepsOldUnitKpiAfterTransfer()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var manager = SeedUser(context, "manager-a", "Manager", unitA.Id);
        var user = SeedUser(context, "snapshot-user", "User", unitA.Id);
        var period = SeedPeriod(context);
        await context.SaveChangesAsync();
        var taskService = TestFactory.CreateTaskService(context);

        var createdTask = await taskService.Create(new CreateTaskDto
        {
            Title = "Department snapshot task"
        }, manager.Id);

        var taskEntity = await context.Tasks.SingleAsync(t => t.Id == createdTask.Id);
        taskEntity.CreatedAt = PeriodStart.AddDays(1);
        user.UnitId = unitB.Id;
        user.JoinedUnitAt = PeriodMiddle;
        SeedHistory(context, user.Id, unitA.Id, "User", PeriodStart, PeriodMiddle);
        SeedHistory(context, user.Id, unitB.Id, "User", PeriodMiddle, null);
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(1, result.TotalTasks);
        Assert.Equal(unitB.Id, result.UnitId);
        Assert.True(result.IsPartialPeriod);
        Assert.Contains("nhieu giai doan", result.PeriodNote);
    }

    [Fact]
    public async Task GetPerformance_WithNoTasks_ReturnsNeutralNewHireScore()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var user = SeedUser(context, "new-user", "User", unit.Id);
        var period = SeedPeriod(context);
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(100, result.Score);
        Assert.Equal("Moi/Thu viec", result.Level);
        Assert.Equal(0, result.TotalTasks);
        Assert.False(result.IsAtRisk);
    }

    [Fact]
    public async Task GetPerformance_WithOnTimeApprovedTask_AddsCompletionBonus()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var user = SeedUser(context, "employee", "User", unit.Id);
        var period = SeedPeriod(context);
        SeedApprovedTask(context, user.Id, manager.Id, unit.Id, "On-time task", PeriodStart.AddDays(5));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(1, result.TotalTasks);
        Assert.Equal(1, result.CompletedOnTime);
        Assert.Equal(5, result.BonusPoints);
        Assert.Equal(105, result.Score);
        Assert.Equal("Xuat sac", result.Level);
    }

    [Fact]
    public async Task GetPerformance_WithDateOnlyDeadlineAndSameDayCompletion_CountsOnTime()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var user = SeedUser(context, "employee", "User", unit.Id);
        var period = SeedPeriod(context);
        var dueDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2026, 7, 6, 18, 0, 0, DateTimeKind.Utc);
        SeedApprovedTaskWithDeadline(context, user.Id, manager.Id, unit.Id, "Same day deadline", dueDate, completedAt);
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(1, result.CompletedOnTime);
        Assert.Equal(0, result.CompletedLate);
        Assert.Equal(105, result.Score);
    }

    [Fact]
    public async Task GetPerformance_WhenBonusesExceedLimit_CapsScoreAt120()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var user = SeedUser(context, "employee", "User", unit.Id);
        var period = SeedPeriod(context);

        for (var i = 0; i < 5; i++)
        {
            SeedApprovedTask(context, user.Id, manager.Id, unit.Id, $"On-time task {i}", PeriodStart.AddDays(2 + i));
        }

        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(5, result.CompletedOnTime);
        Assert.True(result.BonusPoints > 20);
        Assert.Equal(120, result.Score);
    }

    [Fact]
    public async Task GetPerformance_WithThreeOverdueTasks_IsAtRisk()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var manager = SeedUser(context, "manager", "Manager", unit.Id);
        var user = SeedUser(context, "employee", "User", unit.Id);
        var period = SeedPeriod(context);
        SeedOpenTask(context, user.Id, manager.Id, unit.Id, "Overdue 1", PeriodStart.AddDays(1));
        SeedOpenTask(context, user.Id, manager.Id, unit.Id, "Overdue 2", PeriodStart.AddDays(2));
        SeedOpenTask(context, user.Id, manager.Id, unit.Id, "Overdue 3", PeriodStart.AddDays(3));
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        var result = await service.GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal(3, result.TotalTasks);
        Assert.Equal(3, result.OverdueTasks);
        Assert.Equal(25, result.PenaltyPoints);
        Assert.Equal(75, result.Score);
        Assert.True(result.IsAtRisk);
        Assert.Contains("qua han", result.WarningMessage);
    }

    [Fact]
    public async Task Update_WhenUnitChanges_ClosesCurrentHistoryAndCreatesNewActiveSegment()
    {
        await using var context = TestFactory.CreateDbContext();
        var unitA = SeedUnit(context, "Unit A");
        var unitB = SeedUnit(context, "Unit B");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "transfer-user", "User", unitA.Id);
        SeedHistory(context, user.Id, unitA.Id, "User", PeriodStart, null);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = CreateUserService(context, transactions);

        var before = DateTime.UtcNow;
        await service.Update(user.Id, new UpdateUserDto
        {
            RowVersion = user.RowVersion,
            Role = "User",
            UnitId = unitB.Id
        }, admin.Id);
        var after = DateTime.UtcNow;

        var histories = await context.UserWorkHistories
            .Where(h => h.UserId == user.Id)
            .OrderBy(h => h.EffectiveFrom)
            .ToListAsync();

        Assert.Equal(2, histories.Count);
        Assert.Equal(unitA.Id, histories[0].UnitId);
        Assert.NotNull(histories[0].EffectiveTo);
        Assert.Equal(unitB.Id, histories[1].UnitId);
        Assert.Equal("User", histories[1].Role);
        Assert.Null(histories[1].EffectiveTo);
        Assert.Equal(admin.Id, histories[1].ChangedBy);
        Assert.InRange(histories[1].EffectiveFrom, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal(1, transactions.ExecutionCount);
    }

    [Fact]
    public async Task Update_WhenUnitAndRoleDoNotChange_DoesNotCreateDuplicateHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = SeedUnit(context, "Unit A");
        var admin = SeedUser(context, "admin", "Admin", null);
        var user = SeedUser(context, "stable-user", "User", unit.Id);
        SeedHistory(context, user.Id, unit.Id, "User", PeriodStart, null);
        await context.SaveChangesAsync();
        var service = CreateUserService(context);

        await service.Update(user.Id, new UpdateUserDto
        {
            RowVersion = user.RowVersion,
            Role = "User",
            UnitId = unit.Id
        }, admin.Id);

        var histories = await context.UserWorkHistories
            .Where(h => h.UserId == user.Id)
            .ToListAsync();

        var history = Assert.Single(histories);
        Assert.Equal(unit.Id, history.UnitId);
        Assert.Equal("User", history.Role);
        Assert.Null(history.EffectiveTo);
        Assert.Equal(0, user.TokenVersion);
    }

    private static UserService CreateUserService(
        AppDbContext context,
        RecordingTransactionManager? transactionManager = null)
    {
        return TestFactory.CreateUserService(context, transactionManager);
    }

    private static KpiPeriod SeedPeriod(AppDbContext context, string status = "Open")
    {
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = "KPI 07/2026",
            Type = "Monthly",
            StartDate = PeriodStart,
            EndDate = PeriodEnd,
            Status = status
        };
        context.KpiPeriods.Add(period);
        return period;
    }

    private static Unit SeedUnit(AppDbContext context, string name)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = name };
        context.Units.Add(unit);
        return unit;
    }

    private static User SeedUser(AppDbContext context, string username, string role, Guid? unitId, DateTime? joinedUnitAt = null)
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
            JoinedUnitAt = joinedUnitAt ?? PeriodStart,
            IsApproved = true
        };
        context.Users.Add(user);
        return user;
    }

    private static void SeedHistory(AppDbContext context, Guid userId, Guid? unitId, string role, DateTime from, DateTime? to)
    {
        context.UserWorkHistories.Add(new UserWorkHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UnitId = unitId,
            Role = role,
            EffectiveFrom = from,
            EffectiveTo = to,
            ChangeReason = "Test history"
        });
    }

    private static void SeedApprovedTask(AppDbContext context, Guid userId, Guid createdBy, Guid unitId, string title, DateTime completedAt)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "",
            CreatedBy = createdBy,
            CreatedAt = completedAt.AddDays(-2),
            DueDate = completedAt.AddDays(1),
            Status = TaskStatusEnum.Approved,
            UnitId = unitId,
            CompletedAt = completedAt,
            CompletedBy = userId
        };
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId
        });
        context.Progresses.Add(new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId,
            Percent = 100,
            HoursSpent = 1,
            Status = ProgressStatusEnum.Approved,
            UpdatedAt = completedAt
        });
    }

    private static void SeedApprovedTaskWithDeadline(
        AppDbContext context,
        Guid userId,
        Guid createdBy,
        Guid unitId,
        string title,
        DateTime dueDate,
        DateTime completedAt)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "",
            CreatedBy = createdBy,
            CreatedAt = completedAt.AddDays(-2),
            DueDate = dueDate,
            Status = TaskStatusEnum.Approved,
            UnitId = unitId,
            CompletedAt = completedAt,
            CompletedBy = userId
        };
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId
        });
        context.Progresses.Add(new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId,
            Percent = 100,
            HoursSpent = 1,
            Status = ProgressStatusEnum.Approved,
            UpdatedAt = completedAt
        });
    }

    private static void SeedOpenTask(AppDbContext context, Guid userId, Guid createdBy, Guid unitId, string title, DateTime dueDate)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "",
            CreatedBy = createdBy,
            CreatedAt = PeriodStart,
            DueDate = dueDate,
            Status = TaskStatusEnum.InProgress,
            UnitId = unitId
        };
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = userId
        });
    }
}
