using PredictiveBudget.Domain.BudgetPlans;

namespace PredictiveBudget.Application.Common;

public interface IBudgetPlanRepository
{
    Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct);
    Task SaveAsync(BudgetPlan plan, CancellationToken ct);
}
