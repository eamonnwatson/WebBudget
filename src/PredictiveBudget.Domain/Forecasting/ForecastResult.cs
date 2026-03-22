using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

/// <summary>
/// Bundles the chart points, summary statistics, and supporting occurrences for a forecast run.
/// </summary>
public sealed record ForecastResult(
    DateRange Range,
    IReadOnlyList<DailyBalancePoint> DailyPoints,
    ForecastSummary Summary,
    IReadOnlyList<DateOnly> BelowZeroDates,
    IReadOnlyList<CashflowOccurrence> Occurrences);
