using System.Diagnostics;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Scheduling;

public sealed class DeadlineReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeadlineReminderOptions _options;
    private readonly ILogger<DeadlineReminderWorker> _logger;

    public DeadlineReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DeadlineReminderOptions> options,
        ILogger<DeadlineReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IDeadlineReminderService>();
                var result = await service.ProcessDueAsync(stoppingToken);
                BackgroundJobMetrics.RecordDeadlineBatch(
                    result,
                    Stopwatch.GetElapsedTime(startedAt));
                if (result.EventsCreated > 0 ||
                    result.EventsSent > 0 ||
                    result.EventsSuppressed > 0 ||
                    result.Failures > 0)
                {
                    _logger.LogInformation(
                        "Deadline reminder batch evaluated {TaskCount} tasks, created {CreatedCount} events, sent {SentCount}, suppressed {SuppressedCount}, and failed {FailureCount}.",
                        result.TasksEvaluated,
                        result.EventsCreated,
                        result.EventsSent,
                        result.EventsSuppressed,
                        result.Failures);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                BackgroundJobMetrics.RecordDeadlineBatchFailure(
                    Stopwatch.GetElapsedTime(startedAt));
                _logger.LogError(
                    exception,
                    "Deadline reminder batch failed; persisted notification state was retained.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
