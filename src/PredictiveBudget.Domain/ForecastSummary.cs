namespace PredictiveBudget.Domain;

public sealed record ForecastSummary(
    Money MinBalance, DateOnly MinDate,
    Money MaxBalance, DateOnly MaxDate,
    DateOnly? FirstBelowZeroDate);
