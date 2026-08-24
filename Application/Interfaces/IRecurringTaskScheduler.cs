namespace WorkManagementSystem.Application.Interfaces;

public sealed record RecurringTaskProcessingResult(
    int TemplatesProcessed,
    int OccurrencesGenerated,
    int Failures);

public interface IRecurringTaskScheduler
{
    Task<RecurringTaskProcessingResult> ProcessDueAsync(
        CancellationToken cancellationToken = default);
}
