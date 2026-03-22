using PredictiveBudget.Persistence.DependencyInjection;
using PredictiveBudget.Web.Components;

namespace PredictiveBudget.Web.Configuration;

/// <summary>
/// Applies the middleware and endpoint setup for the web application.
/// </summary>
internal static class PredictiveBudgetWebApplicationExtensions
{
    public static async Task ConfigurePredictiveBudgetWebAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.Services.EnsureBudgetDatabaseCreatedAsync();
    }
}
