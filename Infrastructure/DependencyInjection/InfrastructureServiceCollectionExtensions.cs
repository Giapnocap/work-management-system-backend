using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;
using WorkManagementSystem.Infrastructure.Security;
using WorkManagementSystem.Infrastructure.Scheduling;
using WorkManagementSystem.Infrastructure.Storage;

namespace WorkManagementSystem.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string connectionString,
        UploadCleanupOptions uploadCleanupOptions)
    {
        services.AddSingleton(Options.Create(uploadCleanupOptions));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });
        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
        services.AddSingleton<IPasswordHashService, BcryptPasswordHashService>();
        services.AddScoped<UploadOrphanCleaner>();
        services.AddHostedService<RecurringTaskWorker>();
        services.AddHostedService<DeadlineReminderWorker>();

        if (uploadCleanupOptions.Enabled)
            services.AddHostedService<UploadOrphanCleanupWorker>();

        return services;
    }
}
