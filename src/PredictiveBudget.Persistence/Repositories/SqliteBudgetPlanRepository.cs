using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Persistence.Database;
using PredictiveBudget.Persistence.Documents;
using PredictiveBudget.Persistence.Mapping;

namespace PredictiveBudget.Persistence.Repositories;

/// <summary>
/// Stores and retrieves serialized budget plans from SQLite through EF Core.
/// </summary>
public sealed class SqliteBudgetPlanRepository(IDbContextFactory<BudgetDbContext> dbContextFactory) : IBudgetPlanRepository
{
    public async Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var documents = await dbContext.BudgetPlans
            .AsNoTracking()
            .ToListAsync(ct);

        return documents
            .OrderByDescending(plan => plan.UpdatedUtc)
            .Select(BudgetPlanMapper.ToDomain)
            .ToList();
    }

    public async Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var document = await dbContext.BudgetPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.PlanId == planId, ct);

        return document is null ? null : BudgetPlanMapper.ToDomain(document);
    }

    public async Task SaveAsync(BudgetPlan plan, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var existing = await dbContext.BudgetPlans
            .SingleOrDefaultAsync(document => document.PlanId == plan.PlanId, ct);

        var updated = BudgetPlanMapper.ToDocument(plan);

        if (existing is null)
        {
            dbContext.BudgetPlans.Add(updated);
        }
        else
        {
            ApplyChanges(existing, updated);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid planId, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var existing = await dbContext.BudgetPlans
            .SingleOrDefaultAsync(document => document.PlanId == planId, ct);

        if (existing is null)
        {
            return;
        }

        dbContext.BudgetPlans.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
    }

    private static void ApplyChanges(BudgetPlanDocument target, BudgetPlanDocument source)
    {
        target.Name = source.Name;
        target.Currency = source.Currency;
        target.UpdatedUtc = source.UpdatedUtc;
        target.Json = source.Json;
    }
}
