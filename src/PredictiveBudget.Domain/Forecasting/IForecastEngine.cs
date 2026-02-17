using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

public interface IForecastEngine
{
    ForecastResult Forecast(BudgetPlan plan, DateRange range);
}
