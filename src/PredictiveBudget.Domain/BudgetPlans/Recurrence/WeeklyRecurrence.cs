using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans.Recurrence;

public sealed record WeeklyRecurrence(
    int IntervalWeeks,
    IReadOnlySet<Weekday> Weekdays,
    BusinessDayAdjustment BusinessDayAdjustment = BusinessDayAdjustment.None)
    : RecurrenceRule(BusinessDayAdjustment)
{
    public override IEnumerable<DateOnly> Expand(DateOnly from, DateOnly to, DateOnly anchor)
    {
        // Anchor should be a valid occurrence date for the pattern
        // For biweekly Friday: anchor = a real Friday payday
        if (IntervalWeeks <= 0) throw new InvalidOperationException("IntervalWeeks must be >= 1.");
        if (Weekdays.Count == 0) yield break;

        // Find the first date >= from that is on a valid cycle relative to anchor
        var d = from;

        while (d <= to)
        {
            if (Weekdays.Contains(ToWeekday(d.DayOfWeek)) && IsOnIntervalCycle(d, anchor, IntervalWeeks))
                yield return ApplyBusinessDayAdjustment(d, BusinessDayAdjustment);

            d = d.AddDays(1);
        }
    }

    private static bool IsOnIntervalCycle(DateOnly date, DateOnly anchor, int intervalWeeks)
    {
        // Compare whole weeks between anchor and date
        // We treat cycles based on anchor week; simple + reliable for payday use
        var days = date.DayNumber - anchor.DayNumber;
        var weeks = Math.DivRem(days, 7, out _);
        return weeks % intervalWeeks == 0;
    }

    private static Weekday ToWeekday(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => Weekday.Monday,
        DayOfWeek.Tuesday => Weekday.Tuesday,
        DayOfWeek.Wednesday => Weekday.Wednesday,
        DayOfWeek.Thursday => Weekday.Thursday,
        DayOfWeek.Friday => Weekday.Friday,
        DayOfWeek.Saturday => Weekday.Saturday,
        DayOfWeek.Sunday => Weekday.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(d))
    };
}