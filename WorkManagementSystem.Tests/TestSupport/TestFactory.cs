using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Tests.TestSupport;

internal static class TestFactory
{
    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }

    public static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "UNIT_TEST_SECRET_KEY_123456789_ABCDEF_32"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    public static IOptions<JwtOptions> CreateJwtOptions()
        => Options.Create(new JwtOptions
        {
            Key = "UNIT_TEST_SECRET_KEY_123456789_ABCDEF_32",
            Issuer = "WorkManagementSystem.Tests",
            Audience = "WorkManagementSystem.Tests.Client",
            ExpirationMinutes = 180
        });

    public static AuditService CreateAuditService(AppDbContext context)
        => new(context);

    public static AuthService CreateAuthService(AppDbContext context)
        => new(
            context,
            CreateJwtOptions(),
            CreateAuditService(context),
            new EmployeeCodeGenerator(context));

    public static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    public static GenericRepository<T> Repo<T>(AppDbContext context) where T : class
        => new(context);

    public static TaskWorkflowService CreateTaskWorkflowService(AppDbContext context)
    {
        return new TaskWorkflowService(
            Repo<TaskAssignee>(context),
            Repo<User>(context),
            Repo<Progress>(context));
    }

    public static TaskBusinessRuleService CreateTaskBusinessRuleService(AppDbContext context)
    {
        return new TaskBusinessRuleService(
            Repo<User>(context),
            context);
    }

    public static TaskDtoBuilder CreateTaskDtoBuilder(AppDbContext context)
    {
        return new TaskDtoBuilder(
            Repo<TaskAssignee>(context),
            Repo<User>(context),
            Repo<Unit>(context),
            Repo<UploadFile>(context),
            Repo<SubTask>(context),
            CreateMapper());
    }

    public static UserWorkHistoryService CreateUserWorkHistoryService(AppDbContext context)
    {
        return new UserWorkHistoryService(context);
    }

    public static UserTaskAssignmentService CreateUserTaskAssignmentService(AppDbContext context)
    {
        return new UserTaskAssignmentService(
            Repo<TaskItem>(context),
            Repo<TaskAssignee>(context));
    }

    public static UserUnitMembershipService CreateUserUnitMembershipService(AppDbContext context)
    {
        return new UserUnitMembershipService(Repo<UserUnit>(context));
    }

    public static StaffMovementService CreateStaffMovementService(AppDbContext context)
    {
        return new StaffMovementService(
            Repo<User>(context),
            Repo<Unit>(context),
            CreateUserTaskAssignmentService(context),
            CreateUserUnitMembershipService(context),
            CreateUserWorkHistoryService(context),
            CreateAuditService(context));
    }

    public static UserPerformanceService CreateUserPerformanceService(AppDbContext context)
    {
        return new UserPerformanceService(
            Repo<User>(context),
            Repo<TaskItem>(context),
            Repo<TaskAssignee>(context),
            Repo<Progress>(context),
            context,
            new KpiPeriodResolver(context));
    }

    public static ProjectService CreateProjectService(
        AppDbContext context,
        ITransactionManager? transactionManager = null)
    {
        return new ProjectService(
            context,
            new TaskAccessService(context),
            transactionManager ?? new EfTransactionManager(context),
            CreateAuditService(context));
    }

    public static UserService CreateUserService(
        AppDbContext context,
        ITransactionManager? transactionManager = null)
    {
        return new UserService(
            Repo<User>(context),
            Repo<UserUnit>(context),
            CreateMapper(),
            CreateUserTaskAssignmentService(context),
            CreateStaffMovementService(context),
            CreateUserPerformanceService(context),
            transactionManager ?? new EfTransactionManager(context),
            CreateAuditService(context));
    }

    public static TaskService CreateTaskService(
        AppDbContext context,
        INotificationService? notificationService = null,
        ITransactionManager? transactionManager = null)
    {
        return new TaskService(
            Repo<TaskItem>(context),
            Repo<TaskAssignee>(context),
            Repo<User>(context),
            Repo<TaskHistory>(context),
            notificationService ?? new TestNotificationService(),
            new TaskAccessService(context),
            CreateTaskWorkflowService(context),
            CreateTaskBusinessRuleService(context),
            CreateTaskDtoBuilder(context),
            transactionManager ?? new EfTransactionManager(context));
    }

    public static ProgressService CreateProgressService(
        AppDbContext context,
        INotificationService? notificationService = null,
        ITransactionManager? transactionManager = null)
    {
        return new ProgressService(
            Repo<Progress>(context),
            Repo<TaskItem>(context),
            Repo<User>(context),
            Repo<UploadFile>(context),
            Repo<ReportReview>(context),
            Repo<Unit>(context),
            notificationService ?? new TestNotificationService(),
            new TaskAccessService(context),
            CreateTaskWorkflowService(context),
            CreateMapper(),
            transactionManager ?? new EfTransactionManager(context));
    }

    public static ReviewService CreateReviewService(
        AppDbContext context,
        INotificationService? notificationService = null,
        ITransactionManager? transactionManager = null)
    {
        return new ReviewService(
            Repo<Progress>(context),
            Repo<ReportReview>(context),
            Repo<TaskItem>(context),
            notificationService ?? new TestNotificationService(),
            new TaskAccessService(context),
            CreateTaskWorkflowService(context),
            transactionManager ?? new EfTransactionManager(context));
    }
}

internal sealed class TestNotificationService : INotificationService
{
    public List<(Guid UserId, string Message)> Sent { get; } = new();

    public Task AddNotification(Guid userId, string message, CancellationToken cancellationToken = default)
    {
        Sent.Add((userId, message));
        return Task.CompletedTask;
    }

    public Task<List<NotificationDto>> GetMyNotifications(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<NotificationDto>());

    public Task MarkAsRead(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> GetUnreadCount(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class RecordingTransactionManager : ITransactionManager
{
    public int ExecutionCount { get; private set; }
    public int SerializableExecutionCount { get; private set; }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        await operation(cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        return await operation(cancellationToken);
    }

    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        SerializableExecutionCount++;
        return await operation(cancellationToken);
    }
}
