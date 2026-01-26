namespace PredictiveBudget.Domain;

public sealed record ForecastResult(
    DateRange Range,
    IReadOnlyList<DailyBalancePoint> DailyPoints,
    ForecastSummary Summary,
    IReadOnlyList<DateOnly> BelowZeroDates);
