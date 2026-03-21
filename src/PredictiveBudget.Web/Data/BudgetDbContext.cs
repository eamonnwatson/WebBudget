using Microsoft.EntityFrameworkCore;

namespace PredictiveBudget.Web.Data;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetPlanDocument> BudgetPlans => Set<BudgetPlanDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BudgetPlanDocument>(entity =>
        {
            entity.HasKey(x => x.PlanId);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(12);
            entity.Property(x => x.Json).IsRequired();
        });
    }
}
