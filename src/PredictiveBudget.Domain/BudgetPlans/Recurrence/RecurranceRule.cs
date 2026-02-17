using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans.Recurrence;

public abstract record RecurrenceRule(BusinessDayAdjustment BusinessDayAdjustment)
{
    public abstract IEnumerable<DateOnly> Expand(DateOnly from, DateOnly to, DateOnly anchor);

    protected static DateOnly ApplyBusinessDayAdjustment(DateOnly date, BusinessDayAdjustment adj)
    {
        if (adj == BusinessDayAdjustment.None) return date;

        bool IsWeekend(DateOnly d)
            => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (!IsWeekend(date)) return date;

        return adj switch
        {
            BusinessDayAdjustment.NextBusinessDay => NextWeekday(date),
            BusinessDayAdjustment.PreviousBusinessDay => PrevWeekday(date),
            _ => date
        };

        static DateOnly NextWeekday(DateOnly d)
        {
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                d = d.AddDays(1);
            return d;
        }

        static DateOnly PrevWeekday(DateOnly d)
        {
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                d = d.AddDays(-1);
            return d;
        }
    }
}