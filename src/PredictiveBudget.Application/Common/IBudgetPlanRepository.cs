using PredictiveBudget.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace PredictiveBudget.Application.Common;

public interface IBudgetPlanRepository
{
    Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct);
    Task SaveAsync(BudgetPlan plan, CancellationToken ct);
}
