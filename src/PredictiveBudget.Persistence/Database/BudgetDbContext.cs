using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Persistence.Database.Configurations;
using PredictiveBudget.Persistence.Documents;

namespace PredictiveBudget.Persistence.Database;

/// <summary>
/// EF Core context that stores serialized budget plan documents in SQLite.
/// </summary>
public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetPlanDocument> BudgetPlans => Set<BudgetPlanDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BudgetPlanDocumentConfiguration());
    }
}
