using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Common;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;
using WorkManagementSystem.Infrastructure.Security;

namespace WorkManagementSystem.Tests.TestSupport;

internal static class TestFactory
{
    public static AppDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .AddInterceptors(new InMemoryRowVersionInterceptor());

        if (interceptors.Length > 0)
            optionsBuilder.AddInterceptors(interceptors);

        return new AppDbContext(optionsBuilder.Options);
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

    public static BcryptPasswordHashService CreatePasswordHashService()
        => new();

    public static AuthService CreateAuthService(AppDbContext context)
        => new(
            context,
            CreateJwtOptions(),
            CreateAuditService(context),
            new EmployeeCodeGenerator(context),
            CreatePasswordHashService());

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
            CreateAuditService(context),
            context);
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
            transactionManager ?? new EfTransactionManager(context),
            context);
    }

    public static TaskQueryService CreateTaskQueryService(AppDbContext context)
    {
        return new TaskQueryService(
            Repo<TaskItem>(context),
            Repo<TaskAssignee>(context),
            Repo<User>(context),
            Repo<TaskHistory>(context),
            new TaskAccessService(context),
            CreateTaskDtoBuilder(context));
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
            notificationService ?? new TestNotificationService(),
            new TaskAccessService(context),
            CreateTaskWorkflowService(context),
            CreateMapper(),
            transactionManager ?? new EfTransactionManager(context),
            context);
    }

    public static ProgressQueryService CreateProgressQueryService(AppDbContext context)
    {
        return new ProgressQueryService(
            Repo<Progress>(context),
            Repo<TaskItem>(context),
            Repo<User>(context),
            Repo<UploadFile>(context),
            Repo<ReportReview>(context),
            Repo<Unit>(context),
            new TaskAccessService(context),
            CreateMapper());
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
            transactionManager ?? new EfTransactionManager(context),
            context);
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

internal sealed class TestTaskRealtimeNotifier : ITaskRealtimeNotifier
{
    public Task CommentAddedAsync(
        Guid taskId,
        CommentDto comment,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ReactionChangedAsync(
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CommentsSeenAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubTaskAddedAsync(
        Guid taskId,
        SubTaskDto subTask,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubTaskToggledAsync(
        Guid taskId,
        Guid subTaskId,
        bool isCompleted,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubTaskDeletedAsync(
        Guid taskId,
        Guid subTaskId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
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

internal sealed class SaveChangesCounterInterceptor : SaveChangesInterceptor
{
    public int Count { get; private set; }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Count++;
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return ValueTask.FromResult(result);
    }

    public void Reset() => Count = 0;
}

internal sealed class InMemoryRowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateRowVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateRowVersions(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void UpdateRowVersions(DbContext? context)
    {
        if (context == null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<IHasRowVersion>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.RowVersion = Guid.NewGuid().ToByteArray()[..8];
        }
    }
}
