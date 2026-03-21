using PredictiveBudget.Domain.BudgetPlans;

namespace PredictiveBudget.Application.Common;

public interface IBudgetPlanRepository
{
    Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct);
    Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct);
    Task SaveAsync(BudgetPlan plan, CancellationToken ct);
}
