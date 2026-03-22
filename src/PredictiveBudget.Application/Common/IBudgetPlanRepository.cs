using PredictiveBudget.Domain.BudgetPlans;

namespace PredictiveBudget.Application.Common;

/// <summary>
/// Persists budget plans independently of the application service layer.
/// </summary>
public interface IBudgetPlanRepository
{
    /// <summary>
    /// Returns every stored plan in repository-defined order.
    /// </summary>
    Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Loads a single plan when it exists.
    /// </summary>
    Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct);

    /// <summary>
    /// Creates or replaces a stored plan snapshot.
    /// </summary>
    Task SaveAsync(BudgetPlan plan, CancellationToken ct);

    /// <summary>
    /// Removes the plan when it exists.
    /// </summary>
    Task DeleteAsync(Guid planId, CancellationToken ct);
}
