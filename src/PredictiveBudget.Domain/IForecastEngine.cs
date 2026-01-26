namespace PredictiveBudget.Domain;

public interface IForecastEngine
{
    ForecastResult Forecast(BudgetPlan plan, DateRange range);
}
