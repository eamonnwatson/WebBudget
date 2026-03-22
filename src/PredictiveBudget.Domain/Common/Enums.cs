namespace PredictiveBudget.Domain.Common;

/// <summary>
/// Describes whether a cashflow increases or decreases the budget balance.
/// </summary>
public enum TransactionDirection
{
    Inflow = 1,
    Outflow = 2
}

/// <summary>
/// Defines how weekend occurrences should be shifted to business days.
/// </summary>
public enum BusinessDayAdjustment
{
    None = 0,
    NextBusinessDay = 1,
    PreviousBusinessDay = 2
}

/// <summary>
/// Uses a domain-specific weekday enum so recurrence data stays serialization friendly.
/// </summary>
public enum Weekday
{
    Monday = 1,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday
}

/// <summary>
/// Describes how a single occurrence should be altered.
/// </summary>
public enum OverrideAction
{
    Skip = 1,
    MoveToDate = 2,
    ReplaceAmount = 3,
    ReplaceName = 4
}

/// <summary>
/// Identifies the origin of a forecasted occurrence.
/// </summary>
public enum OccurrenceSource
{
    RecurringRule = 1,
    PlannedTransaction = 2
}
