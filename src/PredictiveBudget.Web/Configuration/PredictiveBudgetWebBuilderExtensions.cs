using MudBlazor.Services;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Persistence.DependencyInjection;
using PredictiveBudget.Web.Services;

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

        return builder;
    }

    private static string GetBudgetDbConnectionString(WebApplicationBuilder builder)
    {
        string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);

        return builder.Configuration.GetConnectionString("BudgetDb")
            ?? $"Data Source={Path.Combine(dataDirectory, "predictivebudget.db")}";
    }
}
