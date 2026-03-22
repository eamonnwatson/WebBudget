using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans.Recurrence;

/// <summary>
/// Expands a rule that repeats every N months on a specific day of the month.
/// </summary>
public sealed record MonthlyByDayOfMonthRecurrence(
    int IntervalMonths,
    int DayOfMonth,
    BusinessDayAdjustment BusinessDayAdjustment = BusinessDayAdjustment.None)
    : RecurrenceRule(BusinessDayAdjustment)
{
    public override IEnumerable<DateOnly> Expand(DateOnly from, DateOnly to, DateOnly anchor)
    {
        if (IntervalMonths <= 0) throw new InvalidOperationException("IntervalMonths must be >= 1.");
        if (DayOfMonth is < 1 or > 31) throw new InvalidOperationException("DayOfMonth must be 1..31.");

        // Start from the first day of the visible month so monthly cycling stays consistent.
        var cursor = new DateOnly(from.Year, from.Month, 1);

        while (cursor <= to)
        {
            if (IsOnIntervalCycle(cursor, anchor, IntervalMonths))
            {
                var date = SafeDay(cursor.Year, cursor.Month, DayOfMonth);
                if (date >= from && date <= to)
                    yield return ApplyBusinessDayAdjustment(date, BusinessDayAdjustment);
            }

            cursor = cursor.AddMonths(1);
        }
    }

    private static bool IsOnIntervalCycle(DateOnly monthStart, DateOnly anchor, int intervalMonths)
    {
        var months = (monthStart.Year - anchor.Year) * 12 + (monthStart.Month - anchor.Month);
        return months % intervalMonths == 0;
    }

    private static DateOnly SafeDay(int year, int month, int day)
    {
        var last = DateTime.DaysInMonth(year, month);
        // Clamp long months into shorter ones so "31st" style rules still produce an occurrence.
        var actual = Math.Min(day, last);
        return new DateOnly(year, month, actual);
    }
}
