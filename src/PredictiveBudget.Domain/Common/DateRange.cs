namespace PredictiveBudget.Domain.Common;

/// <summary>
/// Represents an inclusive date range used by recurrence and forecasting logic.
/// </summary>
public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}
