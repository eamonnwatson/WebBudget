namespace PredictiveBudget.Domain.Common;

public enum TransactionDirection { Inflow = 1, Outflow = 2 }

public enum BusinessDayAdjustment
{
    None = 0,
    NextBusinessDay = 1,
    PreviousBusinessDay = 2
}

public enum Weekday
{
    Monday = 1, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public enum OverrideAction
{
    Skip = 1,
    MoveToDate = 2,
    ReplaceAmount = 3,
    ReplaceName = 4
}

public enum OccurrenceSource
{
    RecurringRule = 1,
    PlannedTransaction = 2
}