using Microsoft.Extensions.Options;

namespace WorkManagementSystem.Infrastructure.Storage;

public sealed class UploadOrphanCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UploadCleanupOptions _options;
    private readonly ILogger<UploadOrphanCleanupWorker> _logger;

    public UploadOrphanCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<UploadCleanupOptions> options,
        ILogger<UploadOrphanCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var cleaner = scope.ServiceProvider.GetRequiredService<UploadOrphanCleaner>();
                var deletedCount = await cleaner.DeleteOrphansAsync(stoppingToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Deleted {DeletedCount} orphan upload files.",
                        deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Upload orphan cleanup failed; no retry state was lost.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
