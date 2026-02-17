using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

public sealed record ForecastSummary(
    Money MinBalance, DateOnly MinDate,
    Money MaxBalance, DateOnly MaxDate,
    DateOnly? FirstBelowZeroDate);
