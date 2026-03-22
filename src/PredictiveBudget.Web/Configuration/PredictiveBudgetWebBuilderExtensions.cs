using Microsoft.Data.Sqlite;
using MudBlazor.Services;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Web.Services;
using PredictiveBudget.Persistence.DependencyInjection;

namespace PredictiveBudget.Web.Configuration;

/// <summary>
/// Registers the web app's UI, services, and persistence dependencies.
/// </summary>
internal static class PredictiveBudgetWebBuilderExtensions
{
    public static WebApplicationBuilder AddPredictiveBudgetWeb(this WebApplicationBuilder builder)
    {
        builder.Services.AddMudServices();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddPredictiveBudgetPersistence(GetBudgetDbConnectionString(builder));
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IForecastEngine, ForecastEngine>();
        builder.Services.AddScoped<BudgetPlanService>();
        builder.Services.AddScoped<CalendarSubscriptionService>();

        return builder;
    }

    private static string GetBudgetDbConnectionString(WebApplicationBuilder builder)
    {
        string defaultConnectionString = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "predictivebudget.db")}";
        string connectionString = builder.Configuration.GetConnectionString("BudgetDb") ?? defaultConnectionString;
        EnsureSqliteDirectoryExists(connectionString, builder.Environment.ContentRootPath);
        return connectionString;
    }

    private static void EnsureSqliteDirectoryExists(string connectionString, string contentRootPath)
    {
        var connectionBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(connectionBuilder.DataSource) ||
            string.Equals(connectionBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string databasePath = Path.IsPathRooted(connectionBuilder.DataSource)
            ? connectionBuilder.DataSource
            : Path.GetFullPath(Path.Combine(contentRootPath, connectionBuilder.DataSource));
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
