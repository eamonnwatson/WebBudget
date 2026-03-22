using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

public sealed class ForecastEngine : IForecastEngine
{
    public ForecastResult Forecast(BudgetPlan plan, DateRange range)
    {
        var occurrences = ExpandOccurrences(plan, range);

        // Group to daily net amount
        var dailyNet = occurrences
            .GroupBy(o => o.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(new Money(0m, plan.Currency), (acc, o) =>
                {
                    var signed = o.Direction == TransactionDirection.Inflow ? o.Amount : new Money(-o.Amount.Amount, o.Amount.Currency);
                    return new Money(acc.Amount + signed.Amount, plan.Currency);
                }));

        // Roll forward daily end-of-day balance
        var points = new List<DailyBalancePoint>();
        var balance = plan.StartingBalance;

        // If your BalanceAsOfDate is earlier than range.Start, you can still forecast fine,
        // but most people will set it to "today" or "last reconciled day".
        var date = range.Start;
        while (date <= range.End)
        {
            if (dailyNet.TryGetValue(date, out var net))
                balance = new Money(balance.Amount + net.Amount, plan.Currency);

            points.Add(new DailyBalancePoint(date, balance));
            date = date.AddDays(1);
        }

        var minPoint = points.OrderBy(p => p.EndOfDayBalance.Amount).First();
        var maxPoint = points.OrderByDescending(p => p.EndOfDayBalance.Amount).First();
        var firstBelow = points.FirstOrDefault(p => p.EndOfDayBalance.Amount < 0m)?.Date;

        var belowDates = points.Where(p => p.EndOfDayBalance.Amount < 0m).Select(p => p.Date).ToList();

        return new ForecastResult(
            range,
            points,
            new ForecastSummary(
                minPoint.EndOfDayBalance, minPoint.Date,
                maxPoint.EndOfDayBalance, maxPoint.Date,
                firstBelow),
            belowDates,
            occurrences);
    }

    private static IReadOnlyList<CashflowOccurrence> ExpandOccurrences(BudgetPlan plan, DateRange range)
    {
        var list = new List<CashflowOccurrence>();

        // Expand recurring rules
        foreach (var rule in plan.RecurringRules.Where(r => r.IsActive))
        {
            // Anchor date: use rule.EffectiveStartDate (or a dedicated AnchorDate property if you prefer)
            var dates = rule.Recurrence.Expand(range.Start, range.End, rule.EffectiveStartDate);

            foreach (var d in dates)
            {
                if (!rule.IsEffectiveOn(d)) continue;

                list.Add(new CashflowOccurrence(
                    d,
                    rule.Name,
                    rule.Direction,
                    rule.Amount,
                    OccurrenceSource.RecurringRule,
                    rule.RuleId));
            }
        }

        // Add planned one-offs
        foreach (var txn in plan.PlannedTransactions)
        {
            if (txn.Date < range.Start || txn.Date > range.End) continue;

            list.Add(new CashflowOccurrence(
                txn.Date,
                txn.Name,
                txn.Direction,
                txn.Amount,
                OccurrenceSource.PlannedTransaction,
                txn.TransactionId));
        }

        // Apply overrides
        return ApplyOverrides(plan, list);
    }

    private static IReadOnlyList<CashflowOccurrence> ApplyOverrides(BudgetPlan plan, List<CashflowOccurrence> occurrences)
    {
        // Simple override logic keyed by (Source, SourceId, OriginalDate)
        // You can make this faster later with dictionaries.
        foreach (var ov in plan.Overrides)
        {
            var match = occurrences.FirstOrDefault(o =>
                o.Source == ov.Source &&
                o.SourceId == ov.SourceId &&
                o.Date == ov.OriginalDate);

            if (match == default) continue;

            occurrences.Remove(match);

            if (ov.Action == OverrideAction.Skip)
                continue;

            var newDate = ov.Action == OverrideAction.MoveToDate && ov.NewDate.HasValue ? ov.NewDate.Value : match.Date;
            var newAmount = ov.Action == OverrideAction.ReplaceAmount && ov.NewAmount.HasValue ? ov.NewAmount.Value : match.Amount;
            var newName = ov.Action == OverrideAction.ReplaceName && ov.NewName is not null ? ov.NewName : match.Name;

            occurrences.Add(match with { Date = newDate, Amount = newAmount, Name = newName });
        }

        return occurrences
            .OrderBy(o => o.Date)
            .ThenBy(o => o.Name)
            .ToList();
    }
}
