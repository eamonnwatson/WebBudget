using System.Text.Json;
using System.Text.Json.Serialization;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Persistence;

internal static class BudgetPlanMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static BudgetPlanDocument ToDocument(BudgetPlan plan)
    {
        var snapshot = ToSnapshot(plan);

        return new BudgetPlanDocument
        {
            PlanId = plan.PlanId,
            Name = plan.Name,
            Currency = plan.Currency,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Json = JsonSerializer.Serialize(snapshot, JsonOptions)
        };
    }

    public static BudgetPlan ToDomain(BudgetPlanDocument document)
    {
        var snapshot = JsonSerializer.Deserialize<BudgetPlanSnapshot>(document.Json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize budget plan '{document.PlanId}'.");

        var plan = new BudgetPlan(
            snapshot.PlanId,
            snapshot.Name,
            snapshot.Currency,
            new Money(snapshot.StartingBalanceAmount, snapshot.Currency),
            snapshot.BalanceAsOfDate,
            snapshot.TimeZoneId);

        foreach (var recurringRule in snapshot.RecurringRules)
        {
            plan.AddRecurringRule(new RecurringTransactionRule(
                recurringRule.RuleId,
                plan.PlanId,
                recurringRule.Name,
                recurringRule.Direction,
                new Money(recurringRule.Amount, snapshot.Currency),
                recurringRule.EffectiveStartDate,
                recurringRule.EffectiveEndDate,
                ToRecurrence(recurringRule),
                recurringRule.IsActive,
                recurringRule.DefaultAlertDaysBefore));
        }

        foreach (var transaction in snapshot.PlannedTransactions)
        {
            plan.AddPlannedTransaction(new PlannedTransaction(
                transaction.TransactionId,
                plan.PlanId,
                transaction.Date,
                transaction.Name,
                transaction.Direction,
                new Money(transaction.Amount, snapshot.Currency)));
        }

        foreach (var overrideSnapshot in snapshot.Overrides)
        {
            plan.AddOverride(new OccurrenceOverride(
                overrideSnapshot.OverrideId,
                plan.PlanId,
                overrideSnapshot.Source,
                overrideSnapshot.SourceId,
                overrideSnapshot.OriginalDate,
                overrideSnapshot.Action,
                overrideSnapshot.NewDate,
                overrideSnapshot.NewAmount.HasValue ? new Money(overrideSnapshot.NewAmount.Value, snapshot.Currency) : null,
                overrideSnapshot.NewName));
        }

        return plan;
    }

    private static BudgetPlanSnapshot ToSnapshot(BudgetPlan plan)
        => new()
        {
            PlanId = plan.PlanId,
            Name = plan.Name,
            Currency = plan.Currency,
            StartingBalanceAmount = plan.StartingBalance.Amount,
            BalanceAsOfDate = plan.BalanceAsOfDate,
            TimeZoneId = plan.TimeZoneId,
            RecurringRules = plan.RecurringRules.Select(ToSnapshot).ToList(),
            PlannedTransactions = plan.PlannedTransactions.Select(ToSnapshot).ToList(),
            Overrides = plan.Overrides.Select(ToSnapshot).ToList()
        };

    private static RecurringRuleSnapshot ToSnapshot(RecurringTransactionRule rule)
        => rule.Recurrence switch
        {
            WeeklyRecurrence weekly => new RecurringRuleSnapshot
            {
                RuleId = rule.RuleId,
                Name = rule.Name,
                Direction = rule.Direction,
                Amount = rule.Amount.Amount,
                EffectiveStartDate = rule.EffectiveStartDate,
                EffectiveEndDate = rule.EffectiveEndDate,
                IsActive = rule.IsActive,
                DefaultAlertDaysBefore = rule.DefaultAlertDaysBefore,
                Pattern = RecurrencePatternSnapshot.Weekly,
                IntervalWeeks = weekly.IntervalWeeks,
                Weekdays = weekly.Weekdays.OrderBy(day => day).ToList(),
                BusinessDayAdjustment = weekly.BusinessDayAdjustment
            },
            MonthlyByDayOfMonthRecurrence monthly => new RecurringRuleSnapshot
            {
                RuleId = rule.RuleId,
                Name = rule.Name,
                Direction = rule.Direction,
                Amount = rule.Amount.Amount,
                EffectiveStartDate = rule.EffectiveStartDate,
                EffectiveEndDate = rule.EffectiveEndDate,
                IsActive = rule.IsActive,
                DefaultAlertDaysBefore = rule.DefaultAlertDaysBefore,
                Pattern = RecurrencePatternSnapshot.MonthlyByDayOfMonth,
                IntervalMonths = monthly.IntervalMonths,
                DayOfMonth = monthly.DayOfMonth,
                BusinessDayAdjustment = monthly.BusinessDayAdjustment
            },
            YearlyByMonthsAndDayRecurrence yearly => new RecurringRuleSnapshot
            {
                RuleId = rule.RuleId,
                Name = rule.Name,
                Direction = rule.Direction,
                Amount = rule.Amount.Amount,
                EffectiveStartDate = rule.EffectiveStartDate,
                EffectiveEndDate = rule.EffectiveEndDate,
                IsActive = rule.IsActive,
                DefaultAlertDaysBefore = rule.DefaultAlertDaysBefore,
                Pattern = RecurrencePatternSnapshot.YearlyByMonthsAndDay,
                DayOfMonth = yearly.DayOfMonth,
                Months = yearly.Months.OrderBy(month => month).ToList(),
                BusinessDayAdjustment = yearly.BusinessDayAdjustment
            },
            _ => throw new InvalidOperationException($"Unsupported recurrence type '{rule.Recurrence.GetType().Name}'.")
        };

    private static PlannedTransactionSnapshot ToSnapshot(PlannedTransaction transaction)
        => new()
        {
            TransactionId = transaction.TransactionId,
            Date = transaction.Date,
            Name = transaction.Name,
            Direction = transaction.Direction,
            Amount = transaction.Amount.Amount
        };

    private static OccurrenceOverrideSnapshot ToSnapshot(OccurrenceOverride overrideEntry)
        => new()
        {
            OverrideId = overrideEntry.OverrideId,
            Source = overrideEntry.Source,
            SourceId = overrideEntry.SourceId,
            OriginalDate = overrideEntry.OriginalDate,
            Action = overrideEntry.Action,
            NewDate = overrideEntry.NewDate,
            NewAmount = overrideEntry.NewAmount?.Amount,
            NewName = overrideEntry.NewName
        };

    private static RecurrenceRule ToRecurrence(RecurringRuleSnapshot snapshot)
        => snapshot.Pattern switch
        {
            RecurrencePatternSnapshot.Weekly => new WeeklyRecurrence(
                snapshot.IntervalWeeks ?? 1,
                snapshot.Weekdays.ToHashSet(),
                snapshot.BusinessDayAdjustment),
            RecurrencePatternSnapshot.MonthlyByDayOfMonth => new MonthlyByDayOfMonthRecurrence(
                snapshot.IntervalMonths ?? 1,
                snapshot.DayOfMonth ?? 1,
                snapshot.BusinessDayAdjustment),
            RecurrencePatternSnapshot.YearlyByMonthsAndDay => new YearlyByMonthsAndDayRecurrence(
                snapshot.Months.ToHashSet(),
                snapshot.DayOfMonth ?? 1,
                snapshot.BusinessDayAdjustment),
            _ => throw new InvalidOperationException($"Unsupported recurrence pattern '{snapshot.Pattern}'.")
        };

    private sealed class BudgetPlanSnapshot
    {
        public Guid PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal StartingBalanceAmount { get; set; }
        public DateOnly BalanceAsOfDate { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;
        public List<RecurringRuleSnapshot> RecurringRules { get; set; } = [];
        public List<PlannedTransactionSnapshot> PlannedTransactions { get; set; } = [];
        public List<OccurrenceOverrideSnapshot> Overrides { get; set; } = [];
    }

    private sealed class RecurringRuleSnapshot
    {
        public Guid RuleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TransactionDirection Direction { get; set; }
        public decimal Amount { get; set; }
        public DateOnly EffectiveStartDate { get; set; }
        public DateOnly? EffectiveEndDate { get; set; }
        public bool IsActive { get; set; }
        public int? DefaultAlertDaysBefore { get; set; }
        public RecurrencePatternSnapshot Pattern { get; set; }
        public int? IntervalWeeks { get; set; }
        public List<Weekday> Weekdays { get; set; } = [];
        public int? IntervalMonths { get; set; }
        public List<int> Months { get; set; } = [];
        public int? DayOfMonth { get; set; }
        public BusinessDayAdjustment BusinessDayAdjustment { get; set; }
    }

    private sealed class PlannedTransactionSnapshot
    {
        public Guid TransactionId { get; set; }
        public DateOnly Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public TransactionDirection Direction { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class OccurrenceOverrideSnapshot
    {
        public Guid OverrideId { get; set; }
        public OccurrenceSource Source { get; set; }
        public Guid SourceId { get; set; }
        public DateOnly OriginalDate { get; set; }
        public OverrideAction Action { get; set; }
        public DateOnly? NewDate { get; set; }
        public decimal? NewAmount { get; set; }
        public string? NewName { get; set; }
    }

    private enum RecurrencePatternSnapshot
    {
        Weekly = 1,
        MonthlyByDayOfMonth = 2,
        YearlyByMonthsAndDay = 3
    }
}
