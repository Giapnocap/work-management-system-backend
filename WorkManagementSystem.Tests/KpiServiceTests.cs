using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;

namespace WorkManagementSystem.Tests;

public class KpiServiceTests
{
    [Fact]
    public async Task GetCurrentPeriod_WhenMissing_ThrowsWithoutCreatingPeriod()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCurrentPeriod());

        Assert.Empty(await context.KpiPeriods.ToListAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task GetPeriods_WhenMissing_ReturnsEmptyWithoutCreatingPeriod()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = CreateService(context);

        var periods = await service.GetPeriods();

        Assert.Empty(periods);
        Assert.Empty(await context.KpiPeriods.ToListAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task GetPerformance_WhenPeriodMissing_ThrowsWithoutCreatingPeriod()
    {
        await using var context = TestFactory.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "employee",
            FullName = "Employee",
            EmployeeCode = "EMP0001",
            PasswordHash = "hash",
            Role = "User",
            IsApproved = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateUserPerformanceService(context);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetPerformanceAsync(user.Id));

        Assert.Empty(await context.KpiPeriods.ToListAsync());
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task CreatePeriod_WithInvalidRange_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.CreatePeriod(new CreateKpiPeriodDto
        {
            Name = "Invalid",
            StartDate = new DateTime(2026, 7, 10),
            EndDate = new DateTime(2026, 7, 1)
        }, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreatePeriod_WithOverlappingRange_ThrowsBusinessException()
    {
        await using var context = TestFactory.CreateDbContext();
        context.KpiPeriods.Add(new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = "KPI 07/2026",
            Type = "Monthly",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = "Open"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.CreatePeriod(new CreateKpiPeriodDto
        {
            Name = "Overlap",
            StartDate = new DateTime(2026, 7, 15),
            EndDate = new DateTime(2026, 8, 1)
        }, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreatePeriod_WithDateOnlyRange_NormalizesToFullUtcDays()
    {
        await using var context = TestFactory.CreateDbContext();
        var service = CreateService(context);

        var period = await service.CreatePeriod(new CreateKpiPeriodDto
        {
            Name = "KPI 07/2026",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31)
        }, Guid.NewGuid());

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), period.StartDate);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1), period.EndDate);
    }

    [Fact]
    public async Task CreatePeriod_ExecutesWithSerializableIsolation()
    {
        await using var context = TestFactory.CreateDbContext();
        var transactions = new RecordingTransactionManager();
        var service = CreateService(context, transactions);

        await service.CreatePeriod(new CreateKpiPeriodDto
        {
            Name = "KPI 08/2026",
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 31)
        }, Guid.NewGuid());

        Assert.Equal(1, transactions.SerializableExecutionCount);
        Assert.Equal(1, transactions.ExecutionCount);
    }

    [Fact]
    public async Task LockPeriod_ExecutesWithSerializableIsolation()
    {
        await using var context = TestFactory.CreateDbContext();
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = "KPI 07/2026",
            Type = "Monthly",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = "Open"
        };
        context.KpiPeriods.Add(period);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = CreateService(context, transactions);

        var results = await service.LockPeriod(period.Id, Guid.NewGuid());

        Assert.Empty(results);
        Assert.Equal("Locked", period.Status);
        Assert.Equal(1, transactions.ExecutionCount);
        Assert.Equal(1, transactions.SerializableExecutionCount);
    }

    [Fact]
    public async Task LockPeriod_WhenAlreadyLocked_IsIdempotent()
    {
        await using var context = TestFactory.CreateDbContext();
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = "KPI 07/2026",
            Type = "Monthly",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = "Open"
        };
        context.KpiPeriods.Add(period);
        await context.SaveChangesAsync();
        var transactions = new RecordingTransactionManager();
        var service = CreateService(context, transactions);
        var adminId = Guid.NewGuid();

        await service.LockPeriod(period.Id, adminId);
        await service.LockPeriod(period.Id, adminId);

        Assert.Equal("Locked", period.Status);
        Assert.Empty(await context.KpiResults.ToListAsync());
        Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal(2, transactions.SerializableExecutionCount);
    }

    [Fact]
    public async Task LockPeriod_IncludesDeletedHistoricalUserAndKeepsIdentitySnapshot()
    {
        await using var context = TestFactory.CreateDbContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Historical Unit" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "former-employee",
            FullName = "Former Employee",
            EmployeeCode = "EMP-HISTORY",
            PasswordHash = "hash",
            Role = "User",
            UnitId = unit.Id,
            JoinedUnitAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            IsApproved = true,
            IsDeleted = true
        };
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = "KPI 07/2026",
            Type = "Monthly",
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc),
            Status = "Open"
        };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Historical completed task",
            Description = string.Empty,
            CreatedBy = user.Id,
            CreatedAt = period.StartDate.AddDays(1),
            DueDate = period.StartDate.AddDays(5),
            CompletedAt = period.StartDate.AddDays(4),
            CompletedBy = user.Id,
            Status = Domain.Enums.TaskStatus.Approved,
            UnitId = unit.Id
        };

        context.Units.Add(unit);
        context.Users.Add(user);
        context.KpiPeriods.Add(period);
        context.UserWorkHistories.Add(new UserWorkHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UnitId = unit.Id,
            Role = "User",
            EffectiveFrom = period.StartDate,
            EffectiveTo = period.EndDate,
            ChangeReason = "Historical employment"
        });
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id
        });
        context.Progresses.Add(new Progress
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = user.Id,
            Percent = 100,
            Status = Domain.Enums.ProgressStatus.Approved,
            UpdatedAt = task.CompletedAt!.Value
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var lockedResults = await service.LockPeriod(period.Id, Guid.NewGuid());

        var lockedResult = Assert.Single(lockedResults);
        var snapshot = await context.KpiResults.SingleAsync();
        Assert.Equal(user.Id, lockedResult.UserId);
        Assert.Equal("Former Employee", snapshot.FullNameSnapshot);
        Assert.Equal("EMP-HISTORY", snapshot.EmployeeCodeSnapshot);
        Assert.Equal("Historical Unit", snapshot.UnitNameSnapshot);

        user.FullName = "Renamed User";
        user.EmployeeCode = "EMP-NEW";
        unit.Name = "Renamed Unit";
        await context.SaveChangesAsync();

        var performance = await TestFactory.CreateUserPerformanceService(context)
            .GetPerformanceAsync(user.Id, period.Id);

        Assert.Equal("Former Employee", performance.FullName);
        Assert.Equal("EMP-HISTORY", performance.EmployeeCode);
        Assert.Equal("Historical Unit", performance.UnitName);
        Assert.True(performance.IsLocked);
    }

    private static KpiService CreateService(
        AppDbContext context,
        ITransactionManager? transactionManager = null)
        => new(
            context,
            TestFactory.CreateUserPerformanceService(context),
            transactionManager ?? new EfTransactionManager(context),
            TestFactory.CreateAuditService(context),
            new KpiPeriodResolver(context));
}
