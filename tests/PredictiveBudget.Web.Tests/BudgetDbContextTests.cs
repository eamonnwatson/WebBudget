using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Web.Data;

namespace PredictiveBudget.Web.Tests;

public sealed class BudgetDbContextTests
{
    [Fact]
    public void OnModelCreating_ConfiguresBudgetPlanDocument()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new BudgetDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(BudgetPlanDocument));

        Assert.NotNull(entityType);
        Assert.Equal(nameof(BudgetPlanDocument.PlanId), entityType.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Equal(200, entityType.FindProperty(nameof(BudgetPlanDocument.Name))!.GetMaxLength());
        Assert.Equal(12, entityType.FindProperty(nameof(BudgetPlanDocument.Currency))!.GetMaxLength());
        Assert.False(entityType.FindProperty(nameof(BudgetPlanDocument.Json))!.IsNullable);
    }
}
