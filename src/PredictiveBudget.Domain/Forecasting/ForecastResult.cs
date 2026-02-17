using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

public sealed record ForecastResult(
    DateRange Range,
    IReadOnlyList<DailyBalancePoint> DailyPoints,
    ForecastSummary Summary,
    IReadOnlyList<DateOnly> BelowZeroDates);
