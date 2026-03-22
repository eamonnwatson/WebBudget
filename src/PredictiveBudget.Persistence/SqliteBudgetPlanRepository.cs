using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Domain.BudgetPlans;

namespace PredictiveBudget.Persistence;

public sealed class SqliteBudgetPlanRepository : IBudgetPlanRepository
{
    private readonly IDbContextFactory<BudgetDbContext> dbContextFactory;

    public SqliteBudgetPlanRepository(IDbContextFactory<BudgetDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory;
    }

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
            existing.Name = updated.Name;
            existing.Currency = updated.Currency;
            existing.UpdatedUtc = updated.UpdatedUtc;
            existing.Json = updated.Json;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
