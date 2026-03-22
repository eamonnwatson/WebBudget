using MudBlazor.Services;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Persistence;
using PredictiveBudget.Web.Components;
using PredictiveBudget.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

string connectionString = builder.Configuration.GetConnectionString("BudgetDb")
    ?? $"Data Source={Path.Combine(dataDirectory, "predictivebudget.db")}";

builder.Services.AddPredictiveBudgetPersistence(connectionString);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IForecastEngine, ForecastEngine>();
builder.Services.AddScoped<BudgetPlanService>();

var app = builder.Build();

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
app.Run();
