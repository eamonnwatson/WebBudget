using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Application.Tests.TestSupport;

internal sealed class ApplicationTestContext(DateOnly? today = null, IForecastEngine? forecastEngine = null)
{
    public InMemoryBudgetPlanRepository Repository { get; } = new();

    public BudgetPlanService CreateService()
        => new(Repository, forecastEngine ?? new ForecastEngine(), new FixedClock(today ?? new DateOnly(2026, 3, 20)));
}

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today() => today;
}

internal sealed class InMemoryBudgetPlanRepository : IBudgetPlanRepository
{
    private readonly Dictionary<Guid, BudgetPlan> plans = [];

    public IReadOnlyCollection<BudgetPlan> Plans => plans.Values;

    public Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<BudgetPlan>>(plans.Values.ToList());

    public Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct)
        => Task.FromResult(plans.TryGetValue(planId, out var plan) ? plan : null);

    public Task SaveAsync(BudgetPlan plan, CancellationToken ct)
    {
        plans[plan.PlanId] = plan;
        return Task.CompletedTask;
    }
}
