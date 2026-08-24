using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Services;

public sealed class RecurringScheduleCalculator : IRecurringScheduleCalculator
{
    public void Validate(
        RecurrenceType recurrenceType,
        int interval,
        int? dayOfWeek,
        int? dayOfMonth,
        DateTime nextRunAtUtc)
    {
        if (interval is < 1 or > 365)
            throw new BusinessException("Khoảng lặp phải từ 1 đến 365.");

        if (nextRunAtUtc == default)
            throw new BusinessException("Thời điểm chạy tiếp theo không hợp lệ.");

        var normalizedNextRun = DateTime.SpecifyKind(nextRunAtUtc, DateTimeKind.Utc);
        switch (recurrenceType)
        {
            case RecurrenceType.Daily:
                if (dayOfWeek.HasValue || dayOfMonth.HasValue)
                    throw new BusinessException("Lịch hằng ngày không sử dụng thứ hoặc ngày trong tháng.");
                break;

            case RecurrenceType.Weekly:
                if (!dayOfWeek.HasValue || dayOfWeek is < 0 or > 6 || dayOfMonth.HasValue)
                    throw new BusinessException("Lịch hằng tuần cần một thứ hợp lệ từ 0 đến 6.");
                if ((int)normalizedNextRun.DayOfWeek != dayOfWeek.Value)
                    throw new BusinessException("Thời điểm chạy tiếp theo không khớp với thứ đã chọn.");
                break;

            case RecurrenceType.Monthly:
                if (!dayOfMonth.HasValue || dayOfMonth is < 1 or > 31 || dayOfWeek.HasValue)
                    throw new BusinessException("Lịch hằng tháng cần một ngày hợp lệ từ 1 đến 31.");
                var expectedDay = Math.Min(
                    dayOfMonth.Value,
                    DateTime.DaysInMonth(normalizedNextRun.Year, normalizedNextRun.Month));
                if (normalizedNextRun.Day != expectedDay)
                    throw new BusinessException("Thời điểm chạy tiếp theo không khớp với ngày trong tháng đã chọn.");
                break;

            default:
                throw new BusinessException("Loại lịch lặp không hợp lệ.");
        }
    }

    public DateTime GetNextOccurrenceUtc(
        RecurrenceType recurrenceType,
        int interval,
        int? dayOfMonth,
        DateTime currentOccurrenceUtc)
    {
        var current = DateTime.SpecifyKind(currentOccurrenceUtc, DateTimeKind.Utc);
        return recurrenceType switch
        {
            RecurrenceType.Daily => current.AddDays(interval),
            RecurrenceType.Weekly => current.AddDays(checked(interval * 7)),
            RecurrenceType.Monthly => GetNextMonthlyOccurrence(current, interval, dayOfMonth),
            _ => throw new BusinessException("Loại lịch lặp không hợp lệ.")
        };
    }

    private static DateTime GetNextMonthlyOccurrence(
        DateTime currentOccurrenceUtc,
        int interval,
        int? dayOfMonth)
    {
        if (!dayOfMonth.HasValue || dayOfMonth is < 1 or > 31)
            throw new BusinessException("Ngày trong tháng không hợp lệ.");

        var targetMonth = new DateTime(
            currentOccurrenceUtc.Year,
            currentOccurrenceUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc).AddMonths(interval);
        var targetDay = Math.Min(
            dayOfMonth.Value,
            DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month));

        return new DateTime(
                targetMonth.Year,
                targetMonth.Month,
                targetDay,
                0,
                0,
                0,
                DateTimeKind.Utc)
            .AddTicks(currentOccurrenceUtc.TimeOfDay.Ticks);
    }
}
