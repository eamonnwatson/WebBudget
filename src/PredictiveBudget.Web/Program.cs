using PredictiveBudget.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddPredictiveBudgetWeb();

var app = builder.Build();
await app.ConfigurePredictiveBudgetWebAsync();
app.Run();
