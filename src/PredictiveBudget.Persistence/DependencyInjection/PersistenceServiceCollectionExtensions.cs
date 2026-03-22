using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Persistence.Database;
using PredictiveBudget.Persistence.Repositories;

namespace PredictiveBudget.Persistence.DependencyInjection;

/// <summary>
/// Registers SQLite persistence services for the application.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPredictiveBudgetPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextFactory<BudgetDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IBudgetPlanRepository, SqliteBudgetPlanRepository>();

        return services;
    }

    public static async Task EnsureBudgetDatabaseCreatedAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BudgetDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
