using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

[Trait("Category", "SqlServer")]
public sealed class SqlServerRelationalTests : IClassFixture<SqlServerTestDatabase>
{
    private const int TaskListQueryBudget = 9;
    private const int WorkloadQueryBudget = 4;
    private const int KpiDashboardQueryBudget = 11;
    private const int TaskTimelineQueryBudget = 5;
    private const string UpgradeBaselineMigration = "20260821020506_CentralizeTaskWorkflowStateMachine";

    private readonly SqlServerTestDatabase _database;

    public SqlServerRelationalTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [SqlServerFact]
    public async Task Migrations_FromEmptyDatabase_ApplyCompleteSchema()
    {
        await using var context = _database.CreateContext();

        var expectedMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.NotEmpty(expectedMigrations);
        Assert.Equal(expectedMigrations, appliedMigrations);
        Assert.True(await context.Database.CanConnectAsync());
        var globalReminderPolicy = await context.ReminderPolicies
            .AsNoTracking()
            .SingleAsync(policy => policy.ScopeType == ReminderPolicyScope.Global);
        Assert.Equal(24, globalReminderPolicy.BeforeDueHours);
        Assert.Equal(24, globalReminderPolicy.OverdueEscalationHours);
    }

    [SqlServerFact]
    public async Task Migrations_FromPreInsightsSchema_PreserveAndBackfillKpiData()
    {
        await using var database = await TemporarySqlServerDatabase.CreateAsync(
            _database.ConnectionString,
            "WmsUpgrade");
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(UpgradeBaselineMigration);

        var suffix = Guid.NewGuid().ToString("N");
        var periodStart = new DateTime(2037, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Upgrade SQL {suffix}" };
        var user = CreateUser($"upgrade-user-{suffix}", $"UPGRADE-{suffix}");
        user.UnitId = unit.Id;
        user.JoinedUnitAt = periodStart;
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = $"Upgrade KPI {suffix}",
            StartDate = periodStart,
            EndDate = periodStart.AddMonths(1).AddTicks(-1),
            Status = "Locked",
            CreatedAt = periodStart,
            LockedAt = periodStart.AddMonths(1)
        };
        var resultId = Guid.NewGuid();
        context.Units.Add(unit);
        context.Users.Add(user);
        context.KpiPeriods.Add(period);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [KpiResults]
                ([Id], [PeriodId], [UserId], [UnitId], [Role], [FullNameSnapshot],
                 [EmployeeCodeSnapshot], [UnitNameSnapshot], [EffectiveFrom], [EffectiveTo],
                 [Score], [Level], [TotalTasks], [CompletedOnTime], [CompletedLate],
                 [OverdueTasks], [RejectedReports], [BonusPoints], [PenaltyPoints],
                 [ReviewPenaltyPoints], [UnitAverageScore], [PersonalScore], [IsManagerKpi],
                 [IsAtRisk], [WarningMessage], [CalculatedAt], [LockedAt])
            VALUES
                ({resultId}, {period.Id}, {user.Id}, {unit.Id}, {SystemRoles.User}, {user.FullName},
                 {user.EmployeeCode}, {unit.Name}, {period.StartDate}, {period.EndDate},
                 {82}, {"Tốt"}, {10}, {6}, {1}, {2}, {1}, {0}, {3}, {0}, {78.5}, {82},
                 {false}, {false}, {string.Empty}, {period.EndDate}, {period.LockedAt})
            """);

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var expectedMigrations = context.Database.GetMigrations().ToArray();
        var upgraded = await context.KpiResults
            .AsNoTracking()
            .SingleAsync(result => result.Id == resultId);

        Assert.Equal(expectedMigrations, appliedMigrations);
        Assert.Equal(7, upgraded.CompletedTasks);
        Assert.Equal(1, upgraded.ProgressReportCount);
        Assert.Equal(0m, upgraded.PlannedEffortHours);
        Assert.Equal(0m, upgraded.ActualHours);
        Assert.Equal("1.0", upgraded.FormulaVersion);
        Assert.Equal(user.Id, upgraded.UserId);
        Assert.Equal(unit.Id, upgraded.UnitId);
    }

    [SqlServerFact]
    public async Task UniqueConstraint_RejectsDuplicateUsername()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = _database.CreateContext();
        context.Users.AddRange(
            CreateUser($"duplicate-{suffix}", $"EMP-{suffix}-1"),
            CreateUser($"duplicate-{suffix}", $"EMP-{suffix}-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ForeignKeyConstraint_RejectsMembershipWithoutUserAndUnit()
    {
        await using var context = _database.CreateContext();
        context.UserUnits.Add(new UserUnit
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UnitId = Guid.NewGuid()
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task CheckConstraint_RejectsInvalidKpiPeriodDateRange()
    {
        var today = DateTime.UtcNow.Date;
        await using var context = _database.CreateContext();
        context.KpiPeriods.Add(new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = $"Invalid period {Guid.NewGuid():N}",
            StartDate = today.AddDays(1),
            EndDate = today,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task CheckConstraint_RejectsInconsistentKpiSnapshotMetrics()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var periodStart = new DateTime(2045, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(Math.Abs(Guid.NewGuid().GetHashCode()) % 1000);
        await using var context = _database.CreateContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Invalid KPI {suffix}" };
        var user = CreateUser($"invalid-kpi-{suffix}", $"INVALID-KPI-{suffix}");
        user.UnitId = unit.Id;
        user.JoinedUnitAt = periodStart;
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = $"Invalid KPI {suffix}",
            StartDate = periodStart,
            EndDate = periodStart.AddDays(1),
            Status = "Locked"
        };
        context.Units.Add(unit);
        context.Users.Add(user);
        context.KpiPeriods.Add(period);
        context.KpiResults.Add(new KpiResult
        {
            Id = Guid.NewGuid(),
            PeriodId = period.Id,
            UserId = user.Id,
            UnitId = unit.Id,
            Role = SystemRoles.User,
            FullNameSnapshot = user.FullName,
            EmployeeCodeSnapshot = user.EmployeeCode,
            UnitNameSnapshot = unit.Name,
            EffectiveFrom = period.StartDate,
            EffectiveTo = period.EndDate,
            Score = 100,
            Level = "Invalid",
            TotalTasks = 1,
            CompletedTasks = 2,
            FormulaVersion = "1.0"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Transaction_WhenOperationFails_RollsBackPersistedChanges()
    {
        var unitId = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            var transactionManager = new EfTransactionManager(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                transactionManager.ExecuteAsync(async cancellationToken =>
                {
                    context.Units.Add(new Unit
                    {
                        Id = unitId,
                        Name = $"Rollback {Guid.NewGuid():N}"
                    });
                    await context.SaveChangesAsync(cancellationToken);
                    throw new InvalidOperationException("Force rollback");
                }));
        }

        await using var verificationContext = _database.CreateContext();
        Assert.False(await verificationContext.Units
            .IgnoreQueryFilters()
            .AnyAsync(unit => unit.Id == unitId));
    }

    [SqlServerFact]
    public async Task RowVersion_WhenConcurrentUpdateUsesStaleToken_PreventsLostUpdate()
    {
        var unitId = Guid.NewGuid();
        await using (var setupContext = _database.CreateContext())
        {
            setupContext.Units.Add(new Unit
            {
                Id = unitId,
                Name = $"Concurrency {Guid.NewGuid():N}"
            });
            await setupContext.SaveChangesAsync();
        }

        await using var firstContext = _database.CreateContext();
        await using var staleContext = _database.CreateContext();
        var firstCopy = await firstContext.Units.SingleAsync(unit => unit.Id == unitId);
        var staleCopy = await staleContext.Units.SingleAsync(unit => unit.Id == unitId);

        firstCopy.Name = $"First update {Guid.NewGuid():N}";
        await firstContext.SaveChangesAsync();

        staleCopy.Name = $"Stale update {Guid.NewGuid():N}";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());

        await using var verificationContext = _database.CreateContext();
        var persistedName = await verificationContext.Units
            .Where(unit => unit.Id == unitId)
            .Select(unit => unit.Name)
            .SingleAsync();
        Assert.Equal(firstCopy.Name, persistedName);
    }

    [SqlServerFact]
    public async Task Review_WhenTwoRequestsRace_PersistsExactlyOneDecision()
    {
        var suffix = Guid.NewGuid().ToString("N");
        Guid taskId;
        Guid progressId;
        Guid managerId;
        await using (var setupContext = _database.CreateContext())
        {
            var unit = new Unit { Id = Guid.NewGuid(), Name = $"Review SQL {suffix}" };
            var manager = CreateUser($"review-manager-{suffix}", $"MGR-{suffix}");
            manager.Role = SystemRoles.Manager;
            manager.UnitId = unit.Id;
            var employee = CreateUser($"review-user-{suffix}", $"EMP-{suffix}");
            employee.UnitId = unit.Id;
            var task = CreateTask("Concurrent review", manager.Id, unit.Id);
            task.RequiresReview = true;
            task.Status = TaskStatusEnum.Submitted;
            var progress = new Progress
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = employee.Id,
                Percent = 100,
                HoursSpent = 4,
                Status = ProgressStatus.Submitted,
                UpdatedAt = DateTime.UtcNow
            };

            setupContext.Units.Add(unit);
            setupContext.Users.AddRange(manager, employee);
            setupContext.Tasks.Add(task);
            setupContext.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = employee.Id
            });
            setupContext.Progresses.Add(progress);
            await setupContext.SaveChangesAsync();

            taskId = task.Id;
            progressId = progress.Id;
            managerId = manager.Id;
        }

        var saveBarrier = new TwoPartySaveBarrierInterceptor();
        await using var firstContext = _database.CreateContext(saveBarrier);
        await using var secondContext = _database.CreateContext(saveBarrier);
        var firstService = TestFactory.CreateReviewService(firstContext);
        var secondService = TestFactory.CreateReviewService(secondContext);

        var outcomes = await Task.WhenAll(
            CaptureExceptionAsync(() => firstService.Review(new ReviewDto
            {
                ProgressId = progressId,
                Approve = true,
                Comment = "Approved by first request."
            }, managerId)),
            CaptureExceptionAsync(() => secondService.Review(new ReviewDto
            {
                ProgressId = progressId,
                Approve = true,
                Comment = "Approved by second request."
            }, managerId)));

        Assert.Single(outcomes, outcome => outcome == null);
        var conflict = Assert.Single(outcomes, outcome => outcome != null);
        Assert.True(
            conflict is DbUpdateConcurrencyException or DbUpdateException,
            $"Expected a database conflict, but received {conflict!.GetType().Name}.");

        await using var verificationContext = _database.CreateContext();
        var savedTask = await verificationContext.Tasks
            .AsNoTracking()
            .SingleAsync(task => task.Id == taskId);
        var savedProgress = await verificationContext.Progresses
            .AsNoTracking()
            .SingleAsync(progress => progress.Id == progressId);

        Assert.Equal(TaskStatusEnum.Approved, savedTask.Status);
        Assert.Equal(4, savedTask.ActualHours);
        Assert.Equal(ProgressStatus.Approved, savedProgress.Status);
        Assert.Single(await verificationContext.Reviews
            .AsNoTracking()
            .Where(review => review.ProgressId == progressId)
            .ToListAsync());
        Assert.Single(await verificationContext.TaskHistories
            .AsNoTracking()
            .Where(history =>
                history.RelatedEntityId == progressId &&
                history.FieldName == "ProgressStatus" &&
                history.NewValue == ProgressStatus.Approved.ToString())
            .ToListAsync());
        Assert.Single(await verificationContext.TaskHistories
            .AsNoTracking()
            .Where(history =>
                history.RelatedEntityId == progressId &&
                history.FieldName == "Status" &&
                history.NewValue == TaskStatusEnum.Approved.ToString())
            .ToListAsync());
    }

    [SqlServerFact]
    public async Task TaskDependency_UniqueConstraint_RejectsDuplicateEdge()
    {
        SqlDependencyFixture fixture;
        await using (var setupContext = _database.CreateContext())
            fixture = await SeedDependencyTasksAsync(setupContext);

        await using var firstContext = _database.CreateContext();
        await using var competingContext = _database.CreateContext();
        firstContext.TaskDependencies.Add(
            CreateDependency(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id));
        competingContext.TaskDependencies.Add(
            CreateDependency(fixture.TaskA.Id, fixture.TaskB.Id, fixture.Manager.Id));

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => competingContext.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task TaskDependency_CheckConstraint_RejectsSelfReference()
    {
        await using var context = _database.CreateContext();
        var fixture = await SeedDependencyTasksAsync(context);
        context.TaskDependencies.Add(
            CreateDependency(fixture.TaskA.Id, fixture.TaskA.Id, fixture.Manager.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task TaskDependency_WorkflowAndGraphQueries_TranslateOnSqlServer()
    {
        await using var context = _database.CreateContext();
        var fixture = await SeedDependencyTasksAsync(context);
        var dependencyService = TestFactory.CreateTaskDependencyService(context);
        var workflowService = TestFactory.CreateTaskWorkflowService(context);

        await dependencyService.AddAsync(
            fixture.TaskA.Id,
            fixture.TaskB.Id,
            fixture.Manager.Id);

        var blockedGraph = await dependencyService.GetGraphAsync(
            fixture.TaskA.Id,
            fixture.Manager.Id);
        Assert.True(blockedGraph.Nodes.Single(node => node.Id == fixture.TaskA.Id).IsBlocked);

        await workflowService.ApplyCompletionStateAsync(
            fixture.TaskB,
            fixture.Employee.Id);
        await context.SaveChangesAsync();

        var unblockedHistoryExists = await context.TaskHistories.AnyAsync(history =>
            history.TaskId == fixture.TaskA.Id &&
            history.FieldName == "DependencyUnblocked");
        Assert.True(unblockedHistoryExists);
    }

    [SqlServerFact]
    public async Task UserCapacity_ConstraintsRejectInvalidAndDuplicateOpenPeriods()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = _database.CreateContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Capacity SQL {suffix}" };
        var manager = CreateUser($"capacity-manager-{suffix}", $"MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        var employee = CreateUser($"capacity-user-{suffix}", $"EMP-{suffix}");
        employee.UnitId = unit.Id;
        context.Units.Add(unit);
        context.Users.AddRange(manager, employee);
        await context.SaveChangesAsync();

        context.UserCapacities.Add(new UserCapacity
        {
            Id = Guid.NewGuid(),
            UserId = employee.Id,
            WeeklyCapacityHours = 0m,
            EffectiveFrom = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            ChangedByUserId = manager.Id
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.UserCapacities.AddRange(
            CreateCapacity(employee.Id, manager.Id, 40m, DateTime.UtcNow.Date),
            CreateCapacity(employee.Id, manager.Id, 35m, DateTime.UtcNow.Date.AddDays(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task TaskPlannedEffort_CheckConstraintRejectsNonPositiveValue()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = _database.CreateContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Effort SQL {suffix}" };
        var manager = CreateUser($"effort-manager-{suffix}", $"MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        context.Units.Add(unit);
        context.Users.Add(manager);
        await context.SaveChangesAsync();

        var task = CreateTask("Invalid planned effort", manager.Id, unit.Id);
        task.PlannedEffortHours = 0m;
        context.Tasks.Add(task);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task TaskList_QueryCountDoesNotGrowWithPageSizeOnMediumDataset()
    {
        var commandCounter = new CommandCounterInterceptor();
        await using var context = _database.CreateContext(commandCounter);
        var fixture = await PerformanceDatasetSeeder.SeedTaskListAsync(context);
        context.ChangeTracker.Clear();
        var service = TestFactory.CreateTaskQueryService(context);

        commandCounter.Reset();
        var smallPage = await service.Get(
            string.Empty,
            page: 1,
            size: 10,
            status: null,
            fixture.ManagerId);
        var smallPageCommandCount = commandCounter.ReaderCount;

        context.ChangeTracker.Clear();
        commandCounter.Reset();
        var largePage = await service.Get(
            string.Empty,
            page: 1,
            size: 100,
            status: null,
            fixture.ManagerId);
        var largePageCommandCount = commandCounter.ReaderCount;

        Assert.Equal(fixture.TaskCount, smallPage.Total);
        Assert.Equal(fixture.TaskCount, largePage.Total);
        Assert.Equal(10, smallPage.Data.Count);
        Assert.Equal(100, largePage.Data.Count);
        Assert.All(largePage.Data, task => Assert.NotEmpty(task.Assignees));
        Assert.All(largePage.Data, task => Assert.NotEmpty(task.SubTasks));
        Assert.Equal(smallPageCommandCount, largePageCommandCount);
        Assert.Equal(TaskListQueryBudget, smallPageCommandCount);
    }

    [SqlServerFact]
    public async Task WorkloadQuery_TranslatesAndUsesConstantCommandCount()
    {
        var commandCounter = new CommandCounterInterceptor();
        await using var context = _database.CreateContext(commandCounter);
        var suffix = Guid.NewGuid().ToString("N");
        var weekStart = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = weekStart.AddDays(6);
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Workload SQL {suffix}" };
        var manager = CreateUser($"workload-manager-{suffix}", $"MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        var employees = Enumerable.Range(1, 30)
            .Select(index =>
            {
                var user = CreateUser(
                    $"workload-user-{index}-{suffix}",
                    $"EMP-{index}-{suffix}");
                user.UnitId = unit.Id;
                return user;
            })
            .ToList();

        context.Units.Add(unit);
        context.Users.Add(manager);
        context.Users.AddRange(employees);
        foreach (var employee in employees)
        {
            var task = CreateTask($"SQL workload {employee.EmployeeCode}", manager.Id, unit.Id);
            task.CreatedAt = weekStart;
            task.StartDate = weekStart;
            task.DueDate = weekEnd;
            task.PlannedEffortHours = 20m;
            context.Tasks.Add(task);
            context.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = employee.Id
            });
        }
        await context.SaveChangesAsync();
        commandCounter.Reset();
        var service = TestFactory.CreateWorkloadService(
            context,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero)));

        var result = await service.GetWorkloadAsync(
            manager.Id,
            weekStart,
            weekEnd,
            null);

        Assert.Equal(30, result.Users.Count);
        Assert.All(result.Users, workload => Assert.Equal(50m, workload.WorkloadPercent));
        Assert.Equal(WorkloadQueryBudget, commandCounter.ReaderCount);
    }

    [SqlServerFact]
    public async Task KpiDashboard_OpenPeriodTranslatesAndUsesConstantCommandCountForLargeUnit()
    {
        var commandCounter = new CommandCounterInterceptor();
        await using var context = _database.CreateContext(commandCounter);
        var suffix = Guid.NewGuid().ToString("N");
        var periodStart = new DateTime(2036, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddTicks(-1);
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"KPI SQL {suffix}" };
        var manager = CreateUser($"kpi-manager-{suffix}", $"KPI-MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        manager.JoinedUnitAt = periodStart;
        var period = new KpiPeriod
        {
            Id = Guid.NewGuid(),
            Name = $"KPI SQL {suffix}",
            StartDate = periodStart,
            EndDate = periodEnd,
            Status = "Open"
        };
        var employees = Enumerable.Range(1, 30)
            .Select(index =>
            {
                var employee = CreateUser(
                    $"kpi-user-{index}-{suffix}",
                    $"KPI-{index}-{suffix}");
                employee.UnitId = unit.Id;
                employee.JoinedUnitAt = periodStart;
                return employee;
            })
            .ToList();

        context.Units.Add(unit);
        context.Users.Add(manager);
        context.Users.AddRange(employees);
        context.KpiPeriods.Add(period);
        foreach (var employee in employees)
        {
            var task = CreateTask($"KPI task {employee.EmployeeCode}", manager.Id, unit.Id);
            task.CreatedAt = periodStart;
            task.DueDate = periodStart.AddDays(10);
            task.CompletedAt = periodStart.AddDays(5);
            task.CompletedBy = employee.Id;
            task.Status = TaskStatusEnum.Approved;
            task.PlannedEffortHours = 1m;
            context.Tasks.Add(task);
            context.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = employee.Id
            });
            context.Progresses.Add(new Progress
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = employee.Id,
                Percent = 100,
                HoursSpent = 1m,
                Status = ProgressStatus.Approved,
                UpdatedAt = periodStart.AddDays(5)
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        commandCounter.Reset();
        var service = new KpiService(
            context,
            TestFactory.CreateUserPerformanceService(context),
            new EfTransactionManager(context),
            TestFactory.CreateAuditService(context),
            new KpiPeriodResolver(context));

        var dashboard = await service.GetDashboard(period.Id, manager.Id);

        Assert.Equal(30, dashboard.Users.Count);
        Assert.Equal(30, dashboard.Summary.Throughput);
        Assert.Equal(KpiDashboardQueryBudget, commandCounter.ReaderCount);

        commandCounter.Reset();
        var personalPerformance = await TestFactory.CreateUserPerformanceService(context)
            .GetPerformanceAsync(employees[0].Id, period.Id);

        Assert.Equal(1, personalPerformance.CompletedTasks);
        Assert.Equal(100m, personalPerformance.CompletionRate);
        Assert.Equal(100m, personalPerformance.EstimationAccuracy);
    }

    [SqlServerFact]
    public async Task TaskTimeline_CursorTranslatesAndQueryCountDoesNotGrowWithPageSize()
    {
        var commandCounter = new CommandCounterInterceptor();
        await using var context = _database.CreateContext(commandCounter);
        var suffix = Guid.NewGuid().ToString("N");
        var occurredAt = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Timeline SQL {suffix}" };
        var manager = CreateUser($"timeline-manager-{suffix}", $"MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        var employee = CreateUser($"timeline-user-{suffix}", $"EMP-{suffix}");
        employee.UnitId = unit.Id;
        var task = CreateTask("SQL timeline", manager.Id, unit.Id);

        context.Units.Add(unit);
        context.Users.AddRange(manager, employee);
        context.Tasks.Add(task);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id
        });
        context.TaskComments.AddRange(Enumerable.Range(1, 40).Select(index => new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = employee.Id,
            Content = $"SQL timeline comment {index}",
            CreatedAt = occurredAt
        }));
        var progressTransitionId = Guid.NewGuid();
        context.TaskHistories.Add(new TaskHistory
        {
            Id = progressTransitionId,
            TaskId = task.Id,
            ChangedBy = employee.Id,
            FieldName = "ProgressStatus",
            OldValue = ProgressStatus.InProgress.ToString(),
            NewValue = ProgressStatus.Submitted.ToString(),
            RelatedEntityId = Guid.NewGuid(),
            Reason = "SQL translation verification.",
            ChangedAt = occurredAt.AddMinutes(1)
        });
        await context.SaveChangesAsync();
        var service = TestFactory.CreateTaskTimelineService(context);

        commandCounter.Reset();
        var firstPage = await service.GetTaskTimelineAsync(
            task.Id,
            employee.Id,
            new TaskTimelineQueryDto
            {
                Size = 5,
                Type = TimelineEventTypes.CommentAdded
            });
        var smallPageCommandCount = commandCounter.ReaderCount;

        commandCounter.Reset();
        var secondPage = await service.GetTaskTimelineAsync(
            task.Id,
            employee.Id,
            new TaskTimelineQueryDto
            {
                Size = 5,
                Type = TimelineEventTypes.CommentAdded,
                Cursor = firstPage.NextCursor
            });
        var cursorPageCommandCount = commandCounter.ReaderCount;

        commandCounter.Reset();
        var largePage = await service.GetTaskTimelineAsync(
            task.Id,
            employee.Id,
            new TaskTimelineQueryDto
            {
                Size = 30,
                Type = TimelineEventTypes.CommentAdded
            });
        var largePageCommandCount = commandCounter.ReaderCount;

        Assert.True(firstPage.HasMore);
        Assert.True(secondPage.HasMore);
        Assert.True(largePage.HasMore);
        Assert.Empty(firstPage.Items.Select(item => item.Id).Intersect(secondPage.Items.Select(item => item.Id)));
        Assert.Equal(smallPageCommandCount, cursorPageCommandCount);
        Assert.Equal(smallPageCommandCount, largePageCommandCount);
        Assert.Equal(TaskTimelineQueryBudget, smallPageCommandCount);

        var progressTransitions = await service.GetTaskTimelineAsync(
            task.Id,
            employee.Id,
            new TaskTimelineQueryDto
            {
                Size = 5,
                Type = TimelineEventTypes.ProgressStatusChanged
            });
        var progressTransition = Assert.Single(progressTransitions.Items);
        Assert.Equal(progressTransitionId, progressTransition.Id);
        Assert.Equal("Submitted", progressTransition.Metadata?.Status);
        Assert.Equal("SQL translation verification.", progressTransition.Metadata?.Reason);
    }

    [SqlServerFact]
    public async Task RecurringTaskScheduler_RacingWorkers_CreateExactlyOneOccurrence()
    {
        var now = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        Guid templateId;
        await using (var setupContext = _database.CreateContext())
        {
            await DisableRecurringTemplatesAsync(setupContext);
            var fixture = await RecurringTaskTestData.SeedAsync(setupContext);
            var template = await RecurringTaskTestData.AddTemplateAsync(
                setupContext,
                fixture,
                now.UtcDateTime);
            templateId = template.Id;
        }

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var firstScheduler = TestFactory.CreateRecurringTaskScheduler(
            firstContext,
            new FixedTimeProvider(now));
        var secondScheduler = TestFactory.CreateRecurringTaskScheduler(
            secondContext,
            new FixedTimeProvider(now));

        var results = await Task.WhenAll(
            firstScheduler.ProcessDueAsync(),
            secondScheduler.ProcessDueAsync());

        await using var verificationContext = _database.CreateContext();
        var occurrences = await verificationContext.GeneratedTaskOccurrences
            .Where(occurrence => occurrence.TemplateId == templateId)
            .ToListAsync();
        Assert.Single(occurrences);
        Assert.Equal(1, results.Sum(result => result.OccurrencesGenerated));
        Assert.True(await verificationContext.Tasks.AnyAsync(
            task => task.Id == occurrences[0].TaskId));
    }

    [SqlServerFact]
    public async Task RecurringTaskScheduler_AfterRestart_DoesNotLoseOrRepeatPersistedSchedule()
    {
        var now = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        Guid templateId;
        await using (var setupContext = _database.CreateContext())
        {
            await DisableRecurringTemplatesAsync(setupContext);
            var fixture = await RecurringTaskTestData.SeedAsync(setupContext);
            var template = await RecurringTaskTestData.AddTemplateAsync(
                setupContext,
                fixture,
                now.UtcDateTime);
            templateId = template.Id;
        }

        await using (var firstWorkerContext = _database.CreateContext())
        {
            var scheduler = TestFactory.CreateRecurringTaskScheduler(
                firstWorkerContext,
                new FixedTimeProvider(now));
            Assert.Equal(1, (await scheduler.ProcessDueAsync()).OccurrencesGenerated);
        }

        await using (var restartedWorkerContext = _database.CreateContext())
        {
            var scheduler = TestFactory.CreateRecurringTaskScheduler(
                restartedWorkerContext,
                new FixedTimeProvider(now));
            Assert.Equal(0, (await scheduler.ProcessDueAsync()).OccurrencesGenerated);
        }

        await using var verificationContext = _database.CreateContext();
        var occurrence = await verificationContext.GeneratedTaskOccurrences
            .SingleAsync(candidate => candidate.TemplateId == templateId);
        var templateState = await verificationContext.RecurringTaskTemplates
            .SingleAsync(candidate => candidate.Id == templateId);
        Assert.Equal(now.UtcDateTime.AddDays(1), templateState.NextRunAtUtc);
        Assert.True(await verificationContext.Tasks.AnyAsync(task => task.Id == occurrence.TaskId));
    }

    [SqlServerFact]
    public async Task RecurringTaskScheduler_WhenSavePipelineFails_RollsBackTaskOccurrenceAndSchedule()
    {
        var now = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        Guid templateId;
        Guid managerId;
        await using (var setupContext = _database.CreateContext())
        {
            await DisableRecurringTemplatesAsync(setupContext);
            var fixture = await RecurringTaskTestData.SeedAsync(setupContext);
            var template = await RecurringTaskTestData.AddTemplateAsync(
                setupContext,
                fixture,
                now.UtcDateTime);
            templateId = template.Id;
            managerId = fixture.Manager.Id;
        }

        await using (var failingContext = _database.CreateContext(new ThrowAfterSaveInterceptor()))
        {
            var scheduler = TestFactory.CreateRecurringTaskScheduler(
                failingContext,
                new FixedTimeProvider(now));
            var result = await scheduler.ProcessDueAsync();
            Assert.Equal(1, result.Failures);
        }

        await using var verificationContext = _database.CreateContext();
        Assert.False(await verificationContext.GeneratedTaskOccurrences
            .AnyAsync(occurrence => occurrence.TemplateId == templateId));
        Assert.False(await verificationContext.Tasks
            .AnyAsync(task => task.CreatedBy == managerId));
        var templateState = await verificationContext.RecurringTaskTemplates
            .SingleAsync(template => template.Id == templateId);
        Assert.Equal(now.UtcDateTime, templateState.NextRunAtUtc);
        Assert.Null(templateState.LastGeneratedAtUtc);
    }

    [SqlServerFact]
    public async Task ReminderPolicy_UniqueConstraintRejectsDuplicateUnitScope()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = _database.CreateContext();
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Reminder policy SQL {suffix}" };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        context.ReminderPolicies.AddRange(
            CreateUnitReminderPolicy(unit.Id),
            CreateUnitReminderPolicy(unit.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task DeadlineReminder_RacingWorkers_CreateOneMilestoneAndOneInboxNotification()
    {
        var now = new DateTimeOffset(2036, 8, 20, 10, 0, 0, TimeSpan.Zero);
        Guid taskId;
        Guid employeeId;
        await using (var setupContext = _database.CreateContext())
        {
            var fixture = await DeadlineReminderTestData.SeedAsync(
                setupContext,
                now.UtcDateTime.AddHours(24),
                policyScope: ReminderPolicyScope.Project);
            taskId = fixture.Task.Id;
            employeeId = fixture.Employee.Id;
        }

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var firstWorker = TestFactory.CreateDeadlineReminderService(
            firstContext,
            new FixedTimeProvider(now));
        var secondWorker = TestFactory.CreateDeadlineReminderService(
            secondContext,
            new FixedTimeProvider(now));

        var results = await Task.WhenAll(
            firstWorker.ProcessDueAsync(),
            secondWorker.ProcessDueAsync());

        await using var verificationContext = _database.CreateContext();
        var events = await verificationContext.ScheduledNotifications
            .Where(notification =>
                notification.TaskId == taskId &&
                notification.Type == ScheduledNotificationType.DueSoon)
            .ToListAsync();
        var inboxNotifications = await verificationContext.Notifications
            .Where(notification => notification.UserId == employeeId)
            .ToListAsync();
        Assert.Single(events);
        Assert.Equal(ScheduledNotificationStatus.Sent, events[0].Status);
        Assert.Single(inboxNotifications);
        Assert.Equal(1, results.Sum(result => result.EventsSent));
    }

    [SqlServerFact]
    public async Task DeadlineReminder_AfterRestart_DoesNotRepeatPersistedMilestone()
    {
        var now = new DateTimeOffset(2037, 8, 20, 10, 0, 0, TimeSpan.Zero);
        Guid taskId;
        Guid employeeId;
        await using (var setupContext = _database.CreateContext())
        {
            var fixture = await DeadlineReminderTestData.SeedAsync(
                setupContext,
                now.UtcDateTime.AddHours(24),
                policyScope: ReminderPolicyScope.Project);
            taskId = fixture.Task.Id;
            employeeId = fixture.Employee.Id;
        }

        await using (var firstWorkerContext = _database.CreateContext())
        {
            var firstWorker = TestFactory.CreateDeadlineReminderService(
                firstWorkerContext,
                new FixedTimeProvider(now));
            Assert.Equal(1, (await firstWorker.ProcessDueAsync()).EventsSent);
        }

        await using (var restartedWorkerContext = _database.CreateContext())
        {
            var restartedWorker = TestFactory.CreateDeadlineReminderService(
                restartedWorkerContext,
                new FixedTimeProvider(now));
            Assert.Equal(0, (await restartedWorker.ProcessDueAsync()).EventsSent);
        }

        await using var verificationContext = _database.CreateContext();
        Assert.Single(await verificationContext.ScheduledNotifications
            .Where(notification =>
                notification.TaskId == taskId &&
                notification.Type == ScheduledNotificationType.DueSoon)
            .ToListAsync());
        Assert.Single(await verificationContext.Notifications
            .Where(notification => notification.UserId == employeeId)
            .ToListAsync());
    }

    private static User CreateUser(string username, string employeeCode)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "SQL integration user",
            EmployeeCode = employeeCode,
            PasswordHash = "not-used-by-this-test",
            Role = SystemRoles.User,
            JoinedUnitAt = DateTime.UtcNow,
            IsApproved = true
        };
    }

    private static Task<int> DisableRecurringTemplatesAsync(AppDbContext context)
    {
        return context.RecurringTaskTemplates
            .Where(template => template.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(template => template.IsActive, false));
    }

    private static async Task<SqlDependencyFixture> SeedDependencyTasksAsync(AppDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = $"Dependency SQL {suffix}"
        };
        var manager = CreateUser($"dependency-manager-{suffix}", $"MGR-{suffix}");
        manager.Role = SystemRoles.Manager;
        manager.UnitId = unit.Id;
        var employee = CreateUser($"dependency-user-{suffix}", $"EMP-{suffix}");
        employee.UnitId = unit.Id;
        var taskA = CreateTask("SQL dependent", manager.Id, unit.Id);
        var taskB = CreateTask("SQL predecessor", manager.Id, unit.Id);

        context.Units.Add(unit);
        context.Users.AddRange(manager, employee);
        context.Tasks.AddRange(taskA, taskB);
        context.TaskAssignees.Add(new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = taskB.Id,
            UserId = employee.Id
        });
        await context.SaveChangesAsync();

        return new SqlDependencyFixture(manager, employee, taskA, taskB);
    }

    private static TaskItem CreateTask(string title, Guid managerId, Guid unitId)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedBy = managerId,
            CreatedAt = DateTime.UtcNow,
            UnitId = unitId,
            Status = TaskStatusEnum.NotStarted
        };
    }

    private static TaskDependency CreateDependency(
        Guid taskId,
        Guid dependsOnTaskId,
        Guid managerId)
    {
        return new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = managerId
        };
    }

    private static UserCapacity CreateCapacity(
        Guid userId,
        Guid managerId,
        decimal weeklyHours,
        DateTime effectiveFrom)
    {
        return new UserCapacity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeeklyCapacityHours = weeklyHours,
            EffectiveFrom = effectiveFrom,
            CreatedAt = DateTime.UtcNow,
            ChangedByUserId = managerId
        };
    }

    private static ReminderPolicy CreateUnitReminderPolicy(Guid unitId)
    {
        return new ReminderPolicy
        {
            Id = Guid.NewGuid(),
            ScopeType = ReminderPolicyScope.Unit,
            UnitId = unitId,
            BeforeDueHours = 24,
            OverdueEscalationHours = 24,
            NotifyAssignee = true,
            NotifyManager = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task<Exception?> CaptureExceptionAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class TwoPartySaveBarrierInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
                _release.TrySetResult();

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    private sealed record SqlDependencyFixture(
        User Manager,
        User Employee,
        TaskItem TaskA,
        TaskItem TaskB);
}
