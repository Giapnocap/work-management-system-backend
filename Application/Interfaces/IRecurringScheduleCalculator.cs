using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Interfaces;

public interface IRecurringScheduleCalculator
{
    void Validate(
        RecurrenceType recurrenceType,
        int interval,
        int? dayOfWeek,
        int? dayOfMonth,
        DateTime nextRunAtUtc);

    DateTime GetNextOccurrenceUtc(
        RecurrenceType recurrenceType,
        int interval,
        int? dayOfMonth,
        DateTime currentOccurrenceUtc);
}
