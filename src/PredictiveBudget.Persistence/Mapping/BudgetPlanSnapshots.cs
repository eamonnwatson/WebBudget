using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Persistence.Mapping;

/// <summary>
/// JSON-serializable snapshot of a budget plan aggregate.
/// </summary>
internal sealed class BudgetPlanSnapshot
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

/// <summary>
/// JSON shape for a recurring transaction rule.
/// </summary>
internal sealed class RecurringRuleSnapshot
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

/// <summary>
/// JSON shape for a manually scheduled transaction.
/// </summary>
internal sealed class PlannedTransactionSnapshot
{
    public Guid TransactionId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// JSON shape for an occurrence override.
/// </summary>
internal sealed class OccurrenceOverrideSnapshot
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

/// <summary>
/// Serialization-friendly recurrence discriminator.
/// </summary>
internal enum RecurrencePatternSnapshot
{
    Weekly = 1,
    MonthlyByDayOfMonth = 2,
    YearlyByMonthsAndDay = 3
}
