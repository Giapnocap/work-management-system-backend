using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public sealed record DeadlineReminderProcessingResult(
    int TasksEvaluated,
    int EventsCreated,
    int EventsSent,
    int EventsSuppressed,
    int Failures);

public interface IDeadlineReminderService
{
    Task<DeadlineReminderProcessingResult> ProcessDueAsync(
        CancellationToken cancellationToken = default);

    Task<List<ScheduledNotificationDto>> GetTaskRemindersAsync(
        Guid taskId,
        Guid requesterId,
        CancellationToken cancellationToken = default);
}
