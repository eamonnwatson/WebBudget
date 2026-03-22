using System.Text;
using PredictiveBudget.Persistence.DependencyInjection;
using PredictiveBudget.Web.Components;
using PredictiveBudget.Web.Services;

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
        app.MapGet(
            "/subscriptions/plans/{planId:guid}/{token}.ics",
            async Task<IResult> (Guid planId, string token, CalendarSubscriptionService calendarSubscriptionService, CancellationToken ct) =>
            {
                var calendar = await calendarSubscriptionService.BuildCalendarAsync(planId, token, ct);
                return calendar is null
                    ? Results.NotFound()
                    : Results.Text(calendar, "text/calendar", Encoding.UTF8);
            });

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.Services.EnsureBudgetDatabaseCreatedAsync();
    }
}
