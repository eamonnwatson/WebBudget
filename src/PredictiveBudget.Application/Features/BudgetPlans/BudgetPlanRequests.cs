using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Application.Features.BudgetPlans;

public sealed record CreateBudgetPlanRequest(
    string Name,
    string Currency,
    decimal StartingBalance,
    DateOnly? BalanceAsOfDate,
    string? TimeZoneId);

public sealed record UpdateStartingBalanceRequest(
    decimal Amount,
    DateOnly BalanceAsOfDate);

public sealed record AddPlannedTransactionRequest(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    decimal Amount);

public sealed record UpdatePlannedTransactionRequest(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    decimal Amount);

public sealed record AddOccurrenceOverrideRequest(
    OccurrenceSource Source,
    Guid SourceId,
    DateOnly OriginalDate,
    OverrideAction Action,
    DateOnly? NewDate,
    decimal? NewAmount,
    string? NewName);

public sealed record UpdateOccurrenceOverrideRequest(
    OccurrenceSource Source,
    Guid SourceId,
    DateOnly OriginalDate,
    OverrideAction Action,
    DateOnly? NewDate,
    decimal? NewAmount,
    string? NewName);

public sealed record ForecastRequest(DateOnly Start, DateOnly End);

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

public enum RecurrencePattern
{
    Weekly = 1,
    MonthlyByDayOfMonth = 2,
    YearlyByMonthsAndDay = 3
}
