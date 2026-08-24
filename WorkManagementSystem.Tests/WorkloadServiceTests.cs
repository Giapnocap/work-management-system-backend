using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Tests.TestSupport;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Tests;

public sealed class WorkloadServiceTests
{
    private static readonly DateTime WeekStart = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekEnd = WeekStart.AddDays(6);
    private static readonly FixedTimeProvider Clock = new(
        new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GetWorkload_WhenEmployeeHasNoTask_ReturnsZeroPercent()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        var service = TestFactory.CreateWorkloadService(context, Clock);

        var result = await service.GetWorkloadAsync(
            fixture.Manager.Id,
            WeekStart,
            WeekEnd,
            null);

        var employee = Assert.Single(result.Users);
        Assert.Equal(40m, employee.CapacityHours);
        Assert.Equal(0m, employee.RemainingWorkHours);
        Assert.Equal(0m, employee.WorkloadPercent);
        Assert.Equal("Normal", employee.Level);
        Assert.Equal(0, employee.ActiveTaskCount);
    }

    [Fact]
    public async Task GetWorkload_CountsOnlyActiveOverlappingRemainingEffort()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        AddAssignedTask(
            context,
            fixture,
            fixture.Employee.Id,
            plannedHours: 20m,
            actualHours: 5m,
            status: TaskStatusEnum.InProgress,
            startDate: WeekStart,
            dueDate: WeekEnd);
        AddAssignedTask(
            context,
            fixture,
            fixture.Employee.Id,
            plannedHours: 100m,
            actualHours: 0m,
            status: TaskStatusEnum.Approved,
            startDate: WeekStart,
            dueDate: WeekEnd);
        AddAssignedTask(
            context,
            fixture,
            fixture.Employee.Id,
            plannedHours: 80m,
            actualHours: 0m,
            status: TaskStatusEnum.NotStarted,
            startDate: WeekEnd.AddDays(1),
            dueDate: WeekEnd.AddDays(7));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateWorkloadService(context, Clock);

        var result = await service.GetUserWorkloadAsync(
            fixture.Manager.Id,
            fixture.Employee.Id,
            WeekStart,
            WeekEnd);

        Assert.Equal(15m, result.RemainingWorkHours);
        Assert.Equal(37.5m, result.WorkloadPercent);
        Assert.Equal(1, result.ActiveTaskCount);
    }

    [Fact]
    public async Task GetWorkload_SplitsTaskEffortAcrossDirectAssignees()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context, includeSecondEmployee: true);
        var task = CreateTask(fixture, 20m, 0m, TaskStatusEnum.InProgress, WeekStart, WeekEnd);
        context.Tasks.Add(task);
        context.TaskAssignees.AddRange(
            CreateAssignee(task.Id, fixture.Employee.Id),
            CreateAssignee(task.Id, fixture.SecondEmployee!.Id));
        await context.SaveChangesAsync();
        var service = TestFactory.CreateWorkloadService(context, Clock);

        var result = await service.GetWorkloadAsync(
            fixture.Manager.Id,
            WeekStart,
            WeekEnd,
            null);

        Assert.Equal(2, result.Users.Count);
        Assert.All(result.Users, workload => Assert.Equal(10m, workload.RemainingWorkHours));
        Assert.All(result.Users, workload => Assert.Equal(25m, workload.WorkloadPercent));
    }

    [Theory]
    [InlineData(28, "Busy")]
    [InlineData(36, "Overloaded")]
    public async Task GetWorkload_UsesConfiguredThresholdBoundaries(
        int plannedHours,
        string expectedLevel)
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        AddAssignedTask(
            context,
            fixture,
            fixture.Employee.Id,
            plannedHours,
            0m,
            TaskStatusEnum.NotStarted,
            WeekStart,
            WeekEnd);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateWorkloadService(context, Clock);

        var result = await service.GetUserWorkloadAsync(
            fixture.Manager.Id,
            fixture.Employee.Id,
            WeekStart,
            WeekEnd);

        Assert.Equal(plannedHours / 40m * 100m, result.WorkloadPercent);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Fact]
    public async Task UpdateCapacity_WhenCapacityChangesInsideRange_ProrationUsesHistory()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        var service = TestFactory.CreateWorkloadService(context, Clock);

        await service.UpdateCapacityAsync(fixture.Manager.Id, fixture.Employee.Id, new UpdateUserCapacityDto
        {
            WeeklyCapacityHours = 35m,
            EffectiveFrom = WeekStart
        });
        await service.UpdateCapacityAsync(fixture.Manager.Id, fixture.Employee.Id, new UpdateUserCapacityDto
        {
            WeeklyCapacityHours = 70m,
            EffectiveFrom = WeekStart.AddDays(3)
        });

        var result = await service.GetUserWorkloadAsync(
            fixture.Manager.Id,
            fixture.Employee.Id,
            WeekStart,
            WeekEnd);

        Assert.Equal(55m, result.CapacityHours);
        var histories = context.UserCapacities
            .Where(capacity => capacity.UserId == fixture.Employee.Id)
            .OrderBy(capacity => capacity.EffectiveFrom)
            .ToList();
        Assert.Equal(2, histories.Count);
        Assert.Equal(WeekStart.AddDays(3), histories[0].EffectiveTo);
        Assert.Null(histories[1].EffectiveTo);
    }

    [Fact]
    public async Task PreviewAssignment_CalculatesProjectedWorkloadWithoutBlockingCreation()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        AddAssignedTask(
            context,
            fixture,
            fixture.Employee.Id,
            20m,
            0m,
            TaskStatusEnum.InProgress,
            WeekStart,
            WeekEnd);
        await context.SaveChangesAsync();
        var workloadService = TestFactory.CreateWorkloadService(context, Clock);

        var preview = await workloadService.PreviewAssignmentAsync(
            fixture.Manager.Id,
            new AssignmentPreviewRequestDto
            {
                PlannedEffortHours = 16m,
                StartDate = WeekStart,
                DueDate = WeekEnd,
                UserIds = new List<Guid> { fixture.Employee.Id }
            });

        var assignee = Assert.Single(preview.Assignees);
        Assert.Equal(50m, assignee.CurrentWorkloadPercent);
        Assert.Equal(90m, assignee.ProjectedWorkloadPercent);
        Assert.Equal("Overloaded", assignee.ProjectedLevel);
        Assert.True(preview.HasWarning);

        var taskService = TestFactory.CreateTaskService(
            context,
            workloadService: workloadService);
        var created = await taskService.Create(new CreateTaskDto
        {
            Title = "Task van được tạo khi quá tải",
            StartDate = WeekStart,
            DueDate = WeekEnd,
            PlannedEffortHours = 16m,
            UserIds = new List<Guid> { fixture.Employee.Id }
        }, fixture.Manager.Id);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Single(created.WorkloadWarnings);
        Assert.True(context.Tasks.Any(task => task.Id == created.Id));
    }

    [Fact]
    public async Task GetUserWorkload_ManagerCannotReadEmployeeInAnotherUnit()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        var otherUnit = new Unit { Id = Guid.NewGuid(), Name = "Other workload unit" };
        var otherEmployee = CreateUser(
            "other-workload-user",
            "Other employee",
            "EMP-WL-OTHER",
            SystemRoles.User,
            otherUnit.Id);
        context.Units.Add(otherUnit);
        context.Users.Add(otherEmployee);
        await context.SaveChangesAsync();
        var service = TestFactory.CreateWorkloadService(context, Clock);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetUserWorkloadAsync(
            fixture.Manager.Id,
            otherEmployee.Id,
            WeekStart,
            WeekEnd));
    }

    [Fact]
    public async Task UpdateCapacity_RejectsNonPositiveValue()
    {
        await using var context = TestFactory.CreateDbContext();
        var fixture = await SeedUsersAsync(context);
        var service = TestFactory.CreateWorkloadService(context, Clock);

        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateCapacityAsync(
            fixture.Manager.Id,
            fixture.Employee.Id,
            new UpdateUserCapacityDto { WeeklyCapacityHours = 0m }));
    }

    private static async Task<WorkloadFixture> SeedUsersAsync(
        Infrastructure.Data.AppDbContext context,
        bool includeSecondEmployee = false)
    {
        var unit = new Unit { Id = Guid.NewGuid(), Name = $"Workload {Guid.NewGuid():N}" };
        var manager = CreateUser(
            $"manager-{Guid.NewGuid():N}",
            "Workload Manager",
            $"MGR-{Guid.NewGuid():N}",
            SystemRoles.Manager,
            unit.Id);
        var employee = CreateUser(
            $"employee-{Guid.NewGuid():N}",
            "Workload Employee",
            $"EMP-{Guid.NewGuid():N}",
            SystemRoles.User,
            unit.Id);
        User? secondEmployee = null;
        if (includeSecondEmployee)
        {
            secondEmployee = CreateUser(
                $"employee-{Guid.NewGuid():N}",
                "Second Employee",
                $"EMP-{Guid.NewGuid():N}",
                SystemRoles.User,
                unit.Id);
        }

        context.Units.Add(unit);
        context.Users.AddRange(manager, employee);
        if (secondEmployee != null)
            context.Users.Add(secondEmployee);
        await context.SaveChangesAsync();

        return new WorkloadFixture(unit, manager, employee, secondEmployee);
    }

    private static User CreateUser(
        string username,
        string fullName,
        string employeeCode,
        string role,
        Guid unitId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = fullName,
            EmployeeCode = employeeCode,
            PasswordHash = "not-used",
            Role = role,
            UnitId = unitId,
            JoinedUnitAt = WeekStart,
            IsApproved = true
        };
    }

    private static void AddAssignedTask(
        Infrastructure.Data.AppDbContext context,
        WorkloadFixture fixture,
        Guid employeeId,
        decimal plannedHours,
        decimal actualHours,
        TaskStatusEnum status,
        DateTime startDate,
        DateTime dueDate)
    {
        var task = CreateTask(fixture, plannedHours, actualHours, status, startDate, dueDate);
        context.Tasks.Add(task);
        context.TaskAssignees.Add(CreateAssignee(task.Id, employeeId));
    }

    private static TaskItem CreateTask(
        WorkloadFixture fixture,
        decimal plannedHours,
        decimal actualHours,
        TaskStatusEnum status,
        DateTime startDate,
        DateTime dueDate)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = $"Workload task {Guid.NewGuid():N}",
            CreatedBy = fixture.Manager.Id,
            CreatedAt = WeekStart,
            StartDate = startDate,
            DueDate = dueDate,
            Status = status,
            UnitId = fixture.Unit.Id,
            PlannedEffortHours = plannedHours,
            ActualHours = actualHours
        };
    }

    private static TaskAssignee CreateAssignee(Guid taskId, Guid userId)
    {
        return new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId
        };
    }

    private sealed record WorkloadFixture(
        Unit Unit,
        User Manager,
        User Employee,
        User? SecondEmployee);
}
