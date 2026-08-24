using Microsoft.Extensions.DependencyInjection;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Workflows;

namespace WorkManagementSystem.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddOptions<WorkloadOptions>()
            .BindConfiguration(WorkloadOptions.SectionName)
            .Validate(options => options.IsValid(), "Workload configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<RecurringTaskOptions>()
            .BindConfiguration(RecurringTaskOptions.SectionName)
            .Validate(options => options.IsValid(), "Recurring task configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<DeadlineReminderOptions>()
            .BindConfiguration(DeadlineReminderOptions.SectionName)
            .Validate(options => options.IsValid(), "Deadline reminder configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TaskWorkflowPolicy>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskQueryService, TaskQueryService>();
        services.AddScoped<ITaskTimelineService, TaskTimelineService>();
        services.AddScoped<ITaskDependencyService, TaskDependencyService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IProgressQueryService, ProgressQueryService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IKpiPeriodResolver, KpiPeriodResolver>();
        services.AddScoped<IUserPerformanceService, UserPerformanceService>();
        services.AddScoped<IUserWorkHistoryService, UserWorkHistoryService>();
        services.AddScoped<IUserTaskAssignmentService, UserTaskAssignmentService>();
        services.AddScoped<IUserUnitMembershipService, UserUnitMembershipService>();
        services.AddScoped<IStaffMovementService, StaffMovementService>();
        services.AddSingleton<IUploadFileValidator, UploadFileValidator>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IChangePasswordService, ChangePasswordService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ISubTaskService, SubTaskService>();
        services.AddScoped<ITaskAccessService, TaskAccessService>();
        services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
        services.AddScoped<ITaskBusinessRuleService, TaskBusinessRuleService>();
        services.AddScoped<ITaskDtoBuilder, TaskDtoBuilder>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<IWorkloadService, WorkloadService>();
        services.AddSingleton<IRecurringScheduleCalculator, RecurringScheduleCalculator>();
        services.AddScoped<IRecurringTaskService, RecurringTaskService>();
        services.AddScoped<IRecurringTaskScheduler, RecurringTaskSchedulerService>();
        services.AddScoped<IReminderPolicyService, ReminderPolicyService>();
        services.AddScoped<IDeadlineReminderService, DeadlineReminderService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);

        return services;
    }
}
