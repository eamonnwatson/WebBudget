using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

/// <summary>
/// Captures the forecasted end-of-day balance for a single date.
/// </summary>
public sealed record DailyBalancePoint(DateOnly Date, Money EndOfDayBalance);
