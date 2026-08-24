using System.Diagnostics.Metrics;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Scheduling;

namespace WorkManagementSystem.Tests;

public sealed class BackgroundJobMetricsTests
{
    [Fact]
    public void BatchMetrics_ExposeCountsOutcomesAndDurationsWithStableTags()
    {
        var counters = new List<MetricMeasurement<long>>();
        var histograms = new List<MetricMeasurement<double>>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == BackgroundJobMetrics.MeterName)
                    currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            counters.Add(new MetricMeasurement<long>(instrument.Name, value, CopyTags(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            histograms.Add(new MetricMeasurement<double>(instrument.Name, value, CopyTags(tags))));
        listener.Start();

        BackgroundJobMetrics.RecordRecurringBatch(
            new RecurringTaskProcessingResult(3, 2, 1),
            TimeSpan.FromMilliseconds(25));
        BackgroundJobMetrics.RecordDeadlineBatch(
            new DeadlineReminderProcessingResult(10, 4, 2, 1, 1),
            TimeSpan.FromMilliseconds(40));
        BackgroundJobMetrics.RecordRecurringBatchFailure(TimeSpan.FromMilliseconds(5));

        Assert.Contains(counters, measurement =>
            measurement.Name == "workmanagement.background_job.executions" &&
            measurement.Value == 1 &&
            HasTags(measurement.Tags, "recurring_task", "partial_failure"));
        Assert.Contains(counters, measurement =>
            measurement.Name == "workmanagement.background_job.items" &&
            measurement.Value == 2 &&
            measurement.Tags["job.name"]?.ToString() == "recurring_task" &&
            measurement.Tags["item.kind"]?.ToString() == "occurrence" &&
            measurement.Tags["item.outcome"]?.ToString() == "generated");
        Assert.Contains(counters, measurement =>
            measurement.Name == "workmanagement.background_job.items" &&
            measurement.Value == 2 &&
            measurement.Tags["job.name"]?.ToString() == "deadline_reminder" &&
            measurement.Tags["item.outcome"]?.ToString() == "sent");
        Assert.Contains(counters, measurement =>
            measurement.Name == "workmanagement.background_job.executions" &&
            measurement.Value == 1 &&
            HasTags(measurement.Tags, "recurring_task", "failure"));
        Assert.Contains(histograms, measurement =>
            measurement.Name == "workmanagement.background_job.duration" &&
            measurement.Value == 40 &&
            HasTags(measurement.Tags, "deadline_reminder", "partial_failure"));
        Assert.All(counters, measurement => Assert.DoesNotContain(
            measurement.Tags.Keys,
            key => key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)));
        Assert.All(histograms, measurement => Assert.DoesNotContain(
            measurement.Tags.Keys,
            key => key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasTags(
        IReadOnlyDictionary<string, object?> tags,
        string jobName,
        string outcome)
    {
        return tags["job.name"]?.ToString() == jobName &&
               tags["job.outcome"]?.ToString() == outcome;
    }

    private static IReadOnlyDictionary<string, object?> CopyTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
            result[tag.Key] = tag.Value;
        return result;
    }

    private sealed record MetricMeasurement<T>(
        string Name,
        T Value,
        IReadOnlyDictionary<string, object?> Tags);
}
