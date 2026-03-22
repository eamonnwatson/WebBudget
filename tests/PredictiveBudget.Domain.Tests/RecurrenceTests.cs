using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Tests;

/// <summary>
/// Verifies the recurrence engines that generate dated occurrences.
/// </summary>
public sealed class RecurrenceTests
{
    [Fact]
    public void WeeklyRecurrence_Expand_ReturnsDatesOnConfiguredCycle()
    {
        var recurrence = new WeeklyRecurrence(2, new HashSet<Weekday> { Weekday.Friday });

        var dates = recurrence.Expand(
            new DateOnly(2026, 3, 20),
            new DateOnly(2026, 4, 17),
            new DateOnly(2026, 3, 20)).ToArray();

        Assert.Equal(
            [new DateOnly(2026, 3, 20), new DateOnly(2026, 4, 3), new DateOnly(2026, 4, 17)],
            dates);
    }

    [Fact]
    public void WeeklyRecurrence_Expand_AppliesBusinessDayAdjustment()
    {
        var recurrence = new WeeklyRecurrence(
            1,
            new HashSet<Weekday> { Weekday.Saturday },
            BusinessDayAdjustment.NextBusinessDay);

        var dates = recurrence.Expand(
            new DateOnly(2026, 3, 21),
            new DateOnly(2026, 3, 21),
            new DateOnly(2026, 3, 21)).ToArray();

        Assert.Equal([new DateOnly(2026, 3, 23)], dates);
    }

    [Fact]
    public void WeeklyRecurrence_Expand_ThrowsWhenIntervalIsInvalid()
    {
        var recurrence = new WeeklyRecurrence(0, new HashSet<Weekday> { Weekday.Friday });

        var error = Assert.Throws<InvalidOperationException>(() => recurrence.Expand(
            new DateOnly(2026, 3, 20),
            new DateOnly(2026, 3, 27),
            new DateOnly(2026, 3, 20)).ToArray());

        Assert.Equal("IntervalWeeks must be >= 1.", error.Message);
    }

    [Fact]
    public void MonthlyByDayOfMonthRecurrence_Expand_ClampsMissingDays()
    {
        var recurrence = new MonthlyByDayOfMonthRecurrence(1, 31);

        var dates = recurrence.Expand(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 1, 31)).ToArray();

        Assert.Equal([new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)], dates);
    }

    [Fact]
    public void MonthlyByDayOfMonthRecurrence_Expand_AppliesPreviousBusinessDayAdjustment()
    {
        var recurrence = new MonthlyByDayOfMonthRecurrence(1, 31, BusinessDayAdjustment.PreviousBusinessDay);

        var dates = recurrence.Expand(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31),
            new DateOnly(2026, 1, 31)).ToArray();

        Assert.Equal([new DateOnly(2026, 5, 29)], dates);
    }

    [Fact]
    public void MonthlyByDayOfMonthRecurrence_Expand_ThrowsForInvalidDay()
    {
        var recurrence = new MonthlyByDayOfMonthRecurrence(1, 0);

        var error = Assert.Throws<InvalidOperationException>(() => recurrence.Expand(
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 1, 1)).ToArray());

        Assert.Equal("DayOfMonth must be 1..31.", error.Message);
    }

    [Fact]
    public void YearlyByMonthsAndDayRecurrence_Expand_ReturnsOrderedAdjustedDates()
    {
        var recurrence = new YearlyByMonthsAndDayRecurrence(
            new HashSet<int> { 9, 2 },
            31,
            BusinessDayAdjustment.PreviousBusinessDay);

        var dates = recurrence.Expand(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1)).ToArray();

        Assert.Equal([new DateOnly(2026, 2, 27), new DateOnly(2026, 9, 30)], dates);
    }

    [Fact]
    public void YearlyByMonthsAndDayRecurrence_Expand_ThrowsForInvalidMonth()
    {
        var recurrence = new YearlyByMonthsAndDayRecurrence(new HashSet<int> { 13 }, 10);

        var error = Assert.Throws<InvalidOperationException>(() => recurrence.Expand(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1)).ToArray());

        Assert.Equal("Months must be 1..12.", error.Message);
    }
}
