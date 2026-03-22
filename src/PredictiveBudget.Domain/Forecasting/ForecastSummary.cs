using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

/// <summary>
/// Highlights the key balance milestones inside a forecast window.
/// </summary>
public sealed record ForecastSummary(
    Money MinBalance, DateOnly MinDate,
    Money MaxBalance, DateOnly MaxDate,
    DateOnly? FirstBelowZeroDate);
