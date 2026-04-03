using System.Text;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Web.Services;

/// <summary>
/// Builds read-only iCalendar subscription feeds for forecasted budget activity.
/// </summary>
public sealed class CalendarSubscriptionService(
    BudgetPlanService budgetPlanService,
    IClock clock)
{
    private const int RecentTransactionHistoryDays = 10;

    public static string BuildCalendarPath(Guid planId, string token)
        => $"/subscriptions/plans/{planId}/{token}.ics";

    public async Task<string?> BuildCalendarAsync(Guid planId, string token, CancellationToken ct)
    {
        var plan = await budgetPlanService.GetAsync(planId, ct);
        if (plan is null || !StringComparer.Ordinal.Equals(plan.CalendarSubscriptionToken, token))
        {
            return null;
        }

        var today = clock.Today();
        var historyStart = today.AddDays(-RecentTransactionHistoryDays);
        var endDate = today.AddMonths(12);
        var rangeStart = plan.BalanceAsOfDate < historyStart
            ? plan.BalanceAsOfDate
            : historyStart;

        var forecast = await budgetPlanService.ForecastAsync(
            planId,
            new ForecastRequest(rangeStart, endDate),
            ct);

        var visibleEvents = forecast.Occurrences
            .Zip(BuildRunningBalances(forecast), static (occurrence, runningBalance) => new CalendarEvent(occurrence, runningBalance))
            .Where(entry => entry.Occurrence.Date >= historyStart)
            .ToList();
        var belowZeroRanges = BuildBelowZeroRanges(forecast.DailyPoints, historyStart);

        return BuildCalendar(plan, visibleEvents, belowZeroRanges);
    }

    private static string BuildCalendar(
        BudgetPlan plan,
        IReadOnlyList<CalendarEvent> events,
        IReadOnlyList<BelowZeroRange> belowZeroRanges)
    {
        var builder = new StringBuilder();
        var nowUtc = DateTimeOffset.UtcNow;
        string calendarName = $"{plan.Name} forecast";

        AppendCalendarLine(builder, "BEGIN:VCALENDAR");
        AppendCalendarLine(builder, "VERSION:2.0");
        AppendCalendarLine(builder, "PRODID:-//PredictiveBudget//Subscription Calendar//EN");
        AppendCalendarLine(builder, "CALSCALE:GREGORIAN");
        AppendCalendarLine(builder, $"NAME:{EscapeText(calendarName)}");
        AppendCalendarLine(builder, $"X-WR-CALNAME:{EscapeText(calendarName)}");
        AppendCalendarLine(builder, $"X-WR-TIMEZONE:{EscapeText(plan.TimeZoneId)}");

        foreach (var entry in events)
        {
            var occurrence = entry.Occurrence;
            var alarmLeadDays = Math.Max(0, occurrence.AlertDaysBefore);

            AppendCalendarLine(builder, "BEGIN:VEVENT");
            AppendCalendarLine(builder, $"UID:{BuildUid(plan, occurrence)}");
            AppendCalendarLine(builder, $"DTSTAMP:{nowUtc:yyyyMMdd'T'HHmmss'Z'}");
            AppendCalendarLine(builder, $"SUMMARY:{EscapeText(BuildSummary(occurrence))}");
            AppendCalendarLine(builder, $"DESCRIPTION:{EscapeText(BuildDescription(occurrence, entry.RunningBalance))}");
            AppendCalendarLine(builder, $"DTSTART;VALUE=DATE:{occurrence.Date:yyyyMMdd}");
            AppendCalendarLine(builder, $"DTEND;VALUE=DATE:{occurrence.Date.AddDays(1):yyyyMMdd}");
            AppendCalendarLine(builder, "TRANSP:TRANSPARENT");
            AppendCalendarLine(builder, "BEGIN:VALARM");
            AppendCalendarLine(builder, "ACTION:DISPLAY");
            AppendCalendarLine(builder, $"DESCRIPTION:{EscapeText(occurrence.Name)}");
            AppendCalendarLine(builder, $"TRIGGER:{BuildTrigger(alarmLeadDays)}");
            AppendCalendarLine(builder, "END:VALARM");
            AppendCalendarLine(builder, "END:VEVENT");
        }

        foreach (var range in belowZeroRanges)
        {
            AppendCalendarLine(builder, "BEGIN:VEVENT");
            AppendCalendarLine(builder, $"UID:{BuildBelowZeroUid(plan, range)}");
            AppendCalendarLine(builder, $"DTSTAMP:{nowUtc:yyyyMMdd'T'HHmmss'Z'}");
            AppendCalendarLine(builder, $"SUMMARY:{EscapeText("Projected balance below zero")}");
            AppendCalendarLine(builder, $"DESCRIPTION:{EscapeText(BuildBelowZeroDescription(range))}");
            AppendCalendarLine(builder, $"DTSTART;VALUE=DATE:{range.Start:yyyyMMdd}");
            AppendCalendarLine(builder, $"DTEND;VALUE=DATE:{range.End.AddDays(1):yyyyMMdd}");
            AppendCalendarLine(builder, "TRANSP:TRANSPARENT");
            AppendCalendarLine(builder, "END:VEVENT");
        }

        AppendCalendarLine(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static IReadOnlyList<Money> BuildRunningBalances(ForecastResult forecast)
    {
        var closingBalances = forecast.DailyPoints.ToDictionary(point => point.Date, point => point.EndOfDayBalance);
        var runningBalances = new List<Money>(forecast.Occurrences.Count);

        foreach (var day in forecast.Occurrences.GroupBy(occurrence => occurrence.Date).OrderBy(group => group.Key))
        {
            var dayClosingBalance = closingBalances[day.Key];
            var dayNet = day.Aggregate(0m, static (total, occurrence) =>
                total + (occurrence.Direction == TransactionDirection.Inflow
                    ? occurrence.Amount.Amount
                    : -occurrence.Amount.Amount));
            var runningBalance = new Money(dayClosingBalance.Amount - dayNet, dayClosingBalance.Currency);

            foreach (var occurrence in day)
            {
                var signedAmount = occurrence.Direction == TransactionDirection.Inflow
                    ? occurrence.Amount.Amount
                    : -occurrence.Amount.Amount;

                runningBalance = new Money(runningBalance.Amount + signedAmount, runningBalance.Currency);
                runningBalances.Add(runningBalance);
            }
        }

        return runningBalances;
    }

    private static string BuildUid(BudgetPlan plan, CashflowOccurrence occurrence)
        => $"{plan.PlanId:N}-{occurrence.Source}-{occurrence.SourceId:N}-{occurrence.OriginalDate:yyyyMMdd}@predictivebudget.local";

    private static string BuildBelowZeroUid(BudgetPlan plan, BelowZeroRange range)
        => $"{plan.PlanId:N}-BelowZero-{range.Start:yyyyMMdd}-{range.End:yyyyMMdd}@predictivebudget.local";

    private static string BuildSummary(CashflowOccurrence occurrence)
        => $"{occurrence.Name} ({FormatSignedMoney(occurrence.Direction, occurrence.Amount)})";

    private static string BuildDescription(CashflowOccurrence occurrence, Money runningBalance)
        => $"Source: {GetSourceLabel(occurrence.Source)}\nProjected balance after transaction: {FormatMoney(runningBalance)}";

    private static string BuildBelowZeroDescription(BelowZeroRange range)
        => $"Projected balance stays below zero from {FormatDate(range.Start)} through {FormatDate(range.End)}.\nLowest balance in this stretch: {FormatMoney(range.MinBalance)}";

    private static string BuildTrigger(int alertDaysBefore)
        => alertDaysBefore == 0
            ? "-PT0M"
            : $"-P{alertDaysBefore}D";

    private static string GetSourceLabel(OccurrenceSource source)
        => source switch
        {
            OccurrenceSource.RecurringRule => "Recurring rule",
            OccurrenceSource.PlannedTransaction => "Planned transaction",
            _ => source.ToString()
        };

    private static string FormatMoney(Money money)
        => $"{money.Amount:N2} {money.Currency}";

    private static string FormatDate(DateOnly date)
        => date.ToString("MMM d, yyyy");

    private static string FormatSignedMoney(TransactionDirection direction, Money money)
    {
        string sign = direction == TransactionDirection.Outflow ? "-" : "+";
        return $"{sign}{money.Amount:N2} {money.Currency}";
    }

    private static string EscapeText(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void AppendCalendarLine(StringBuilder builder, string line)
    {
        const int maxLineLength = 75;

        if (line.Length <= maxLineLength)
        {
            builder.Append(line).Append("\r\n");
            return;
        }

        builder.Append(line.AsSpan(0, maxLineLength)).Append("\r\n");
        int index = maxLineLength;

        while (index < line.Length)
        {
            int remaining = line.Length - index;
            int length = Math.Min(maxLineLength - 1, remaining);
            builder.Append(' ').Append(line.AsSpan(index, length)).Append("\r\n");
            index += length;
        }
    }

    private sealed record CalendarEvent(CashflowOccurrence Occurrence, Money RunningBalance);

    private sealed record BelowZeroRange(DateOnly Start, DateOnly End, Money MinBalance);

    private static IReadOnlyList<BelowZeroRange> BuildBelowZeroRanges(IReadOnlyList<DailyBalancePoint> points, DateOnly visibleStart)
    {
        var visiblePoints = points
            .Where(point => point.Date >= visibleStart && point.EndOfDayBalance.Amount < 0m)
            .OrderBy(point => point.Date)
            .ToList();

        if (visiblePoints.Count == 0)
        {
            return [];
        }

        var ranges = new List<BelowZeroRange>();
        var rangeStart = visiblePoints[0].Date;
        var rangeEnd = visiblePoints[0].Date;
        var minBalance = visiblePoints[0].EndOfDayBalance;

        for (int index = 1; index < visiblePoints.Count; index++)
        {
            var point = visiblePoints[index];
            bool isContiguous = point.Date == rangeEnd.AddDays(1);

            if (!isContiguous)
            {
                ranges.Add(new BelowZeroRange(rangeStart, rangeEnd, minBalance));
                rangeStart = point.Date;
                rangeEnd = point.Date;
                minBalance = point.EndOfDayBalance;
                continue;
            }

            rangeEnd = point.Date;
            if (point.EndOfDayBalance.Amount < minBalance.Amount)
            {
                minBalance = point.EndOfDayBalance;
            }
        }

        ranges.Add(new BelowZeroRange(rangeStart, rangeEnd, minBalance));
        return ranges;
    }
}
