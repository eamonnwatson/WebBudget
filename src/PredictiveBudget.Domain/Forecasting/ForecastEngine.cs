using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

/// <summary>
/// Expands plan activity into dated occurrences and rolls it into daily balance projections.
/// </summary>
public sealed class ForecastEngine : IForecastEngine
{
    public ForecastResult Forecast(BudgetPlan plan, DateRange range)
    {
        // Forecasting may need to expand earlier than the visible window so the opening balance is accurate.
        var expansionRange = plan.BalanceAsOfDate < range.Start
            ? new DateRange(plan.BalanceAsOfDate, range.End)
            : range;
        var occurrences = ExpandOccurrences(plan, expansionRange);
        var visibleOccurrences = occurrences
            .Where(occurrence => occurrence.Date >= range.Start && occurrence.Date <= range.End)
            .ToList();

        // Collapse same-day occurrences into a single signed net amount for balance rolling.
        var dailyNet = visibleOccurrences
            .GroupBy(o => o.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(new Money(0m, plan.Currency), (acc, o) =>
                {
                    var signed = o.Direction == TransactionDirection.Inflow ? o.Amount : new Money(-o.Amount.Amount, o.Amount.Currency);
                    return new Money(acc.Amount + signed.Amount, plan.Currency);
                }));

        // Walk every day in the requested window so the chart always has contiguous points.
        var points = new List<DailyBalancePoint>();
        var balance = RollForwardToRangeStart(plan, occurrences, range.Start);

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
            visibleOccurrences);
    }

    private static Money RollForwardToRangeStart(BudgetPlan plan, IReadOnlyList<CashflowOccurrence> occurrences, DateOnly rangeStart)
    {
        var balance = plan.StartingBalance;

        if (plan.BalanceAsOfDate >= rangeStart)
        {
            return balance;
        }

        foreach (var occurrence in occurrences.Where(occurrence => occurrence.Date >= plan.BalanceAsOfDate && occurrence.Date < rangeStart))
        {
            balance = occurrence.Direction == TransactionDirection.Inflow
                ? new Money(balance.Amount + occurrence.Amount.Amount, plan.Currency)
                : new Money(balance.Amount - occurrence.Amount.Amount, plan.Currency);
        }

        return balance;
    }

    private static IReadOnlyList<CashflowOccurrence> ExpandOccurrences(BudgetPlan plan, DateRange range)
    {
        var list = new List<CashflowOccurrence>();

        // Expand recurring rules into concrete dated occurrences before layering in manual transactions.
        foreach (var rule in plan.RecurringRules.Where(r => r.IsActive))
        {
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

        // Planned transactions are already concrete occurrences, so they only need range filtering.
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

        // Overrides are applied last so they can replace the final generated occurrence details.
        return ApplyOverrides(plan, list);
    }

    private static IReadOnlyList<CashflowOccurrence> ApplyOverrides(BudgetPlan plan, List<CashflowOccurrence> occurrences)
    {
        // Match overrides to the generated occurrence they target, then rewrite that single instance.
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
