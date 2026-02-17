using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans.Recurrence;

public sealed record YearlyByMonthsAndDayRecurrence(
    IReadOnlySet<int> Months, // e.g. { 2, 9 }
    int DayOfMonth,
    BusinessDayAdjustment BusinessDayAdjustment = BusinessDayAdjustment.None)
    : RecurrenceRule(BusinessDayAdjustment)
{
    public override IEnumerable<DateOnly> Expand(DateOnly from, DateOnly to, DateOnly anchor)
    {
        if (Months.Count == 0) yield break;
        if (Months.Any(m => m < 1 || m > 12)) throw new InvalidOperationException("Months must be 1..12.");
        if (DayOfMonth is < 1 or > 31) throw new InvalidOperationException("DayOfMonth must be 1..31.");

        for (int year = from.Year; year <= to.Year; year++)
        {
            foreach (var month in Months.OrderBy(m => m))
            {
                var date = SafeDay(year, month, DayOfMonth);
                if (date >= from && date <= to)
                    yield return ApplyBusinessDayAdjustment(date, BusinessDayAdjustment);
            }
        }
    }

    private static DateOnly SafeDay(int year, int month, int day)
    {
        var last = DateTime.DaysInMonth(year, month);
        var actual = Math.Min(day, last);
        return new DateOnly(year, month, actual);
    }
}
