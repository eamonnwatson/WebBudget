using PredictiveBudget.Web.Configuration;

// Bootstraps the interactive Blazor app and applies the shared startup configuration.
var builder = WebApplication.CreateBuilder(args);
builder.AddPredictiveBudgetWeb();

var app = builder.Build();
await app.ConfigurePredictiveBudgetWebAsync();
app.Run();
