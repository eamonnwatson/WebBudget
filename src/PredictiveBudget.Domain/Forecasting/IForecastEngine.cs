using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

/// <summary>
/// Produces balance projections for a budget plan.
/// </summary>
public interface IForecastEngine
{
    /// <summary>
    /// Forecasts the supplied plan over the requested date range.
    /// </summary>
    ForecastResult Forecast(BudgetPlan plan, DateRange range);
}
