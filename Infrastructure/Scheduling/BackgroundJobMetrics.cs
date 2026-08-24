using System.Diagnostics;
using System.Diagnostics.Metrics;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Infrastructure.Scheduling;

public static class BackgroundJobMetrics
{
    public const string MeterName = "WorkManagementSystem.BackgroundJobs";

    private const string RecurringTaskJob = "recurring_task";
    private const string DeadlineReminderJob = "deadline_reminder";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> BatchCounter = Meter.CreateCounter<long>(
        "workmanagement.background_job.executions",
        unit: "{execution}",
        description: "Number of background job batch executions.");
    private static readonly Counter<long> ItemCounter = Meter.CreateCounter<long>(
        "workmanagement.background_job.items",
        unit: "{item}",
        description: "Number of items handled by background jobs.");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "workmanagement.background_job.duration",
        unit: "ms",
        description: "Background job batch execution duration in milliseconds.");

    public static void RecordRecurringBatch(
        RecurringTaskProcessingResult result,
        TimeSpan elapsed)
    {
        var outcome = result.Failures > 0 ? "partial_failure" : "success";
        RecordBatch(RecurringTaskJob, outcome, elapsed);
        RecordItems(RecurringTaskJob, "template", "processed", result.TemplatesProcessed);
        RecordItems(RecurringTaskJob, "occurrence", "generated", result.OccurrencesGenerated);
        RecordItems(RecurringTaskJob, "template", "failed", result.Failures);
    }

    public static void RecordDeadlineBatch(
        DeadlineReminderProcessingResult result,
        TimeSpan elapsed)
    {
        var outcome = result.Failures > 0 ? "partial_failure" : "success";
        RecordBatch(DeadlineReminderJob, outcome, elapsed);
        RecordItems(DeadlineReminderJob, "task", "evaluated", result.TasksEvaluated);
        RecordItems(DeadlineReminderJob, "event", "created", result.EventsCreated);
        RecordItems(DeadlineReminderJob, "event", "sent", result.EventsSent);
        RecordItems(DeadlineReminderJob, "event", "suppressed", result.EventsSuppressed);
        RecordItems(DeadlineReminderJob, "event", "failed", result.Failures);
    }

    public static void RecordRecurringBatchFailure(TimeSpan elapsed)
        => RecordBatch(RecurringTaskJob, "failure", elapsed);

    public static void RecordDeadlineBatchFailure(TimeSpan elapsed)
        => RecordBatch(DeadlineReminderJob, "failure", elapsed);

    private static void RecordBatch(string jobName, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { "job.name", jobName },
            { "job.outcome", outcome }
        };
        BatchCounter.Add(1, tags);
        Duration.Record(elapsed.TotalMilliseconds, tags);
    }

    private static void RecordItems(
        string jobName,
        string itemKind,
        string outcome,
        int count)
    {
        if (count <= 0)
            return;

        ItemCounter.Add(
            count,
            new TagList
            {
                { "job.name", jobName },
                { "item.kind", itemKind },
                { "item.outcome", outcome }
            });
    }
}
