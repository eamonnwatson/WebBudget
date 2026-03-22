using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Persistence;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

public sealed class SqliteBudgetPlanRepositoryTests
{
    [Fact]
    public async Task SaveAsync_AndGetAsync_PersistAndReloadPlan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupContext = new BudgetDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var repository = new SqliteBudgetPlanRepository(new TestDbContextFactory(options));
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(150m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");

        await repository.SaveAsync(plan, CancellationToken.None);
        var loaded = await repository.GetAsync(plan.PlanId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(plan.PlanId, loaded.PlanId);
        Assert.Equal("Household", loaded.Name);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingDocument()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupContext = new BudgetDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var repository = new SqliteBudgetPlanRepository(new TestDbContextFactory(options));
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(150m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");

        await repository.SaveAsync(plan, CancellationToken.None);
        plan.SetStartingBalance(new Money(300m, "CAD"), new DateOnly(2026, 3, 25));
        await repository.SaveAsync(plan, CancellationToken.None);

        var loaded = await repository.GetAsync(plan.PlanId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(300m, loaded.StartingBalance.Amount);
        Assert.Equal(new DateOnly(2026, 3, 25), loaded.BalanceAsOfDate);
    }

    [Fact]
    public async Task ListAsync_ReturnsPlansOrderedByMostRecentlyUpdated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlite(connection)
            .Options;
        var olderPlan = new BudgetPlan(
            Guid.NewGuid(),
            "Older",
            "CAD",
            new Money(1m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        var newerPlan = new BudgetPlan(
            Guid.NewGuid(),
            "Newer",
            "CAD",
            new Money(1m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        var olderDocument = BudgetPlanMapper.ToDocument(olderPlan);
        olderDocument.UpdatedUtc = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
        var newerDocument = BudgetPlanMapper.ToDocument(newerPlan);
        newerDocument.UpdatedUtc = new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero);

        await using (var context = new BudgetDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.BudgetPlans.AddRange(olderDocument, newerDocument);
            await context.SaveChangesAsync();
        }

        var repository = new SqliteBudgetPlanRepository(new TestDbContextFactory(options));

        var plans = await repository.ListAsync(CancellationToken.None);

        Assert.Equal(["Newer", "Older"], plans.Select(plan => plan.Name).ToArray());
    }
}
