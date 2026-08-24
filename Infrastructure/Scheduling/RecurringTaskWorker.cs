using System.Diagnostics;
using Microsoft.Extensions.Options;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Scheduling;

public sealed class RecurringTaskWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecurringTaskOptions _options;
    private readonly ILogger<RecurringTaskWorker> _logger;

    public RecurringTaskWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RecurringTaskOptions> options,
        ILogger<RecurringTaskWorker> logger)
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
                var scheduler = scope.ServiceProvider.GetRequiredService<IRecurringTaskScheduler>();
                var result = await scheduler.ProcessDueAsync(stoppingToken);
                BackgroundJobMetrics.RecordRecurringBatch(
                    result,
                    Stopwatch.GetElapsedTime(startedAt));
                if (result.TemplatesProcessed > 0 || result.Failures > 0)
                {
                    _logger.LogInformation(
                        "Recurring task batch processed {TemplateCount} templates, generated {OccurrenceCount} occurrences, and had {FailureCount} failures.",
                        result.TemplatesProcessed,
                        result.OccurrencesGenerated,
                        result.Failures);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                BackgroundJobMetrics.RecordRecurringBatchFailure(
                    Stopwatch.GetElapsedTime(startedAt));
                _logger.LogError(exception, "Recurring task batch failed; persisted schedules were not lost.");
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
