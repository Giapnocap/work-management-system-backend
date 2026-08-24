using WorkManagementSystem.Application.Exceptions;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Tests;

public sealed class RecurringScheduleCalculatorTests
{
    private readonly RecurringScheduleCalculator _calculator = new();

    [Fact]
    public void Daily_PreservesUtcTimeAndAddsInterval()
    {
        var current = new DateTime(2026, 8, 20, 9, 30, 15, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc(
            RecurrenceType.Daily,
            3,
            null,
            current);

        Assert.Equal(current.AddDays(3), next);
        Assert.Equal(DateTimeKind.Utc, next.Kind);
    }

    [Fact]
    public void Weekly_AddsWholeWeekIntervals()
    {
        var current = new DateTime(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc(
            RecurrenceType.Weekly,
            2,
            null,
            current);

        Assert.Equal(current.AddDays(14), next);
    }

    [Theory]
    [InlineData(2024, 1, 31, 2024, 2, 29)]
    [InlineData(2024, 2, 29, 2024, 3, 31)]
    [InlineData(2025, 1, 31, 2025, 2, 28)]
    public void Monthly_ClampsToLastDayWithoutLosingPreferredDay(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var current = new DateTime(year, month, day, 10, 45, 0, DateTimeKind.Utc);

        var next = _calculator.GetNextOccurrenceUtc(
            RecurrenceType.Monthly,
            1,
            31,
            current);

        Assert.Equal(
            new DateTime(expectedYear, expectedMonth, expectedDay, 10, 45, 0, DateTimeKind.Utc),
            next);
    }

    [Fact]
    public void Validate_WhenWeeklyDateDoesNotMatchDay_Throws()
    {
        var monday = new DateTime(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc);

        Assert.Throws<BusinessException>(() => _calculator.Validate(
            RecurrenceType.Weekly,
            1,
            dayOfWeek: 2,
            dayOfMonth: null,
            monday));
    }
}
