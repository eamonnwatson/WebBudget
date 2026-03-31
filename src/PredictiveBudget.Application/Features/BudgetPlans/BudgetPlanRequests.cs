using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Application.Features.BudgetPlans;

/// <summary>
/// Captures the data required to create a new budget plan.
/// </summary>
public sealed record CreateBudgetPlanRequest(
    string Name,
    string Currency,
    decimal StartingBalance,
    DateOnly? BalanceAsOfDate,
    string? TimeZoneId);

/// <summary>
/// Represents an updated reconciled balance checkpoint for an existing plan.
/// </summary>
public sealed record UpdateStartingBalanceRequest(
    decimal Amount,
    DateOnly BalanceAsOfDate);

/// <summary>
/// Captures editable top-level details for an existing budget plan.
/// </summary>
public sealed record UpdateBudgetPlanRequest(
    string Name,
    decimal StartingBalance,
    DateOnly BalanceAsOfDate,
    string? TimeZoneId);

/// <summary>
/// Captures a one-off cashflow entry that should be added to a plan.
/// </summary>
public sealed record AddPlannedTransactionRequest(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    decimal Amount);

/// <summary>
/// Captures edits to an existing planned transaction.
/// </summary>
public sealed record UpdatePlannedTransactionRequest(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    decimal Amount);

/// <summary>
/// Describes an occurrence override to add to a plan.
/// </summary>
public sealed record AddOccurrenceOverrideRequest(
    OccurrenceSource Source,
    Guid SourceId,
    DateOnly OriginalDate,
    OverrideAction Action,
    DateOnly? NewDate,
    decimal? NewAmount,
    string? NewName);

/// <summary>
/// Describes changes to an existing occurrence override.
/// </summary>
public sealed record UpdateOccurrenceOverrideRequest(
    OccurrenceSource Source,
    Guid SourceId,
    DateOnly OriginalDate,
    OverrideAction Action,
    DateOnly? NewDate,
    decimal? NewAmount,
    string? NewName);

/// <summary>
/// Defines the date window to forecast for a plan.
/// </summary>
public sealed record ForecastRequest(DateOnly Start, DateOnly End);

/// <summary>
/// Captures the inputs for adding a recurring transaction rule.
/// </summary>
public sealed record AddRecurringRuleRequest(
    string Name,
    TransactionDirection Direction,
    decimal Amount,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    RecurrencePattern Pattern,
    int IntervalWeeks,
    IReadOnlyCollection<Weekday> Weekdays,
    int IntervalMonths,
    IReadOnlyCollection<int> Months,
    int DayOfMonth,
    BusinessDayAdjustment BusinessDayAdjustment,
    bool IsActive,
    int? DefaultAlertDaysBefore);

/// <summary>
/// Captures the inputs for updating a recurring transaction rule.
/// </summary>
public sealed record UpdateRecurringRuleRequest(
    string Name,
    TransactionDirection Direction,
    decimal Amount,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    RecurrencePattern Pattern,
    int IntervalWeeks,
    IReadOnlyCollection<Weekday> Weekdays,
    int IntervalMonths,
    IReadOnlyCollection<int> Months,
    int DayOfMonth,
    BusinessDayAdjustment BusinessDayAdjustment,
    bool IsActive,
    int? DefaultAlertDaysBefore);

/// <summary>
/// Enumerates the recurrence strategies supported by the application layer.
/// </summary>
public enum RecurrencePattern
{
    Weekly = 1,
    MonthlyByDayOfMonth = 2,
    YearlyByMonthsAndDay = 3
}
