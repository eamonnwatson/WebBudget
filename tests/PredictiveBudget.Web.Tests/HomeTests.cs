using MudBlazor;
using NSubstitute;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Web.Features.BudgetPlans.Dashboard;
using PredictiveBudget.Web.Features.BudgetPlans.Models;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

/// <summary>
/// Exercises the dashboard component's loading, forecasting, and quick-action behavior.
/// </summary>
public sealed class HomeTests
{
    [Fact]
    public async Task OnInitializedAsync_LoadsPlansSelectsTheFirstPlanAndRunsForecast()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        await service.CreateAsync(
            new CreateBudgetPlanRequest("Travel", "USD", 200m, new DateOnly(2026, 4, 1), "America/New_York"),
            CancellationToken.None);
        var expectedPlan = (await service.ListAsync(CancellationToken.None)).First();
        var component = CreateComponent(service);

        await ReflectionTestHelper.InvokeAsync(component, "OnInitializedAsync");

        var plans = ReflectionTestHelper.GetPrivateField<List<BudgetPlan>>(component, "_plans");
        var selectedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_selectedPlan");
        var forecastForm = ReflectionTestHelper.GetPrivateField<ForecastFormModel>(component, "_forecastForm");
        var forecastResult = ReflectionTestHelper.GetPrivateField<ForecastResult>(component, "_forecastResult");
        var expectedStartDate = DateTime.Today;

        Assert.Equal(2, plans.Count);
        Assert.Equal(expectedPlan.PlanId, selectedPlan.PlanId);
        Assert.Equal(expectedStartDate, forecastForm.StartDate);
        Assert.Equal(expectedStartDate.AddDays(365), forecastForm.EndDate);
        Assert.Equal(DateOnly.FromDateTime(expectedStartDate), forecastResult.Range.Start);
        Assert.False(ReflectionTestHelper.GetPrivateField<bool>(component, "_isLoading"));
    }

    [Fact]
    public async Task ChangeSelectedPlanAsync_SwitchesPlanAndResetsForecastWindow()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var secondPlan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Travel", "USD", 200m, new DateOnly(2026, 4, 10), "America/New_York"),
            CancellationToken.None);
        var component = CreateComponent(service);
        await ReflectionTestHelper.InvokeAsync(component, "OnInitializedAsync");

        await ReflectionTestHelper.InvokeAsync(component, "ChangeSelectedPlanAsync", secondPlan.PlanId);

        var selectedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_selectedPlan");
        var forecastForm = ReflectionTestHelper.GetPrivateField<ForecastFormModel>(component, "_forecastForm");

        Assert.Equal(secondPlan.PlanId, selectedPlan.PlanId);
        Assert.Equal(DateTime.Today, forecastForm.StartDate);
        Assert.Equal(DateTime.Today.AddDays(365), forecastForm.EndDate);
    }

    [Fact]
    public async Task CreatePlanAsync_CreatesPlanResetsFormAndSelectsNewPlan()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var component = CreateComponent(service);
        ReflectionTestHelper.SetPrivateField(component, "_planForm", new CreateBudgetPlanFormModel
        {
            Name = "Trip",
            Currency = "usd",
            StartingBalance = 50m,
            BalanceAsOfDate = new DateTime(2026, 3, 22),
            TimeZoneId = "America/New_York"
        });

        await ReflectionTestHelper.InvokeAsync(component, "CreatePlanAsync");

        var plans = await service.ListAsync(CancellationToken.None);
        var createdPlan = Assert.Single(plans);
        var selectedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_selectedPlan");
        var forecastResult = ReflectionTestHelper.GetPrivateField<ForecastResult>(component, "_forecastResult");
        var resetForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_planForm");

        Assert.Equal("USD", createdPlan.Currency);
        Assert.Equal(createdPlan.PlanId, selectedPlan.PlanId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), forecastResult.Range.Start);
        Assert.Equal("CAD", resetForm.Currency);
        Assert.Equal(string.Empty, resetForm.Name);
    }

    [Fact]
    public void OpenCreatePlanModal_ResetsFormAndShowsCreateModal()
    {
        var component = CreateComponent(new WebBudgetPlanContext().CreateService());
        ReflectionTestHelper.SetPrivateField(component, "_planForm", new CreateBudgetPlanFormModel
        {
            Name = "Existing",
            Currency = "USD",
            StartingBalance = 99m,
            BalanceAsOfDate = new DateTime(2026, 3, 22),
            TimeZoneId = "America/New_York"
        });

        ReflectionTestHelper.InvokeVoid(component, "OpenCreatePlanModal");

        var resetForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_planForm");

        Assert.True(ReflectionTestHelper.GetPrivateField<bool>(component, "_showPlanModal"));
        Assert.Equal("CAD", resetForm.Currency);
        Assert.Equal(string.Empty, resetForm.Name);
    }

    [Fact]
    public async Task DeletePlanAsync_RemovesSelectedPlanAndFallsBackToRemainingPlan()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var firstPlan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var secondPlan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Travel", "USD", 200m, new DateOnly(2026, 4, 10), "America/New_York"),
            CancellationToken.None);
        var component = CreateComponent(service);
        await ReflectionTestHelper.InvokeAsync(component, "OnInitializedAsync");
        await ReflectionTestHelper.InvokeAsync(component, "ChangeSelectedPlanAsync", secondPlan.PlanId);
        ReflectionTestHelper.InvokeVoid(component, "OpenDeletePlanModal");

        await ReflectionTestHelper.InvokeAsync(component, "DeletePlanAsync");

        var plans = ReflectionTestHelper.GetPrivateField<List<BudgetPlan>>(component, "_plans");
        var selectedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_selectedPlan");

        Assert.Single(plans);
        Assert.Equal(firstPlan.PlanId, selectedPlan.PlanId);
        Assert.DoesNotContain(plans, plan => plan.PlanId == secondPlan.PlanId);
        Assert.False(ReflectionTestHelper.GetPrivateField<bool>(component, "_showDeletePlanModal"));
    }

    [Fact]
    public void HelperMethods_FormatValuesForDisplay()
    {
        Assert.Equal(
            new DateOnly(2026, 3, 20),
            ReflectionTestHelper.InvokeStatic<DateOnly>(typeof(Home), "ToDateOnly", new DateTime(2026, 3, 20)));
        Assert.Equal(
            $"{123.45m:N2} CAD",
            ReflectionTestHelper.InvokeStatic<string>(typeof(Home), "FormatMoney", new Money(123.45m, "CAD")));
        Assert.Equal(
            "Mar 20, 2026",
            ReflectionTestHelper.InvokeStatic<string>(typeof(Home), "FormatDate", new DateOnly(2026, 3, 20)));
    }

    [Fact]
    public async Task OpenEditPlanModal_LoadsSelectedPlanIntoEditor()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = CreateComponent(service);
        await ReflectionTestHelper.InvokeAsync(component, "OnInitializedAsync");

        ReflectionTestHelper.InvokeVoid(component, "OpenEditPlanModal");

        var planForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_planForm");

        Assert.True(ReflectionTestHelper.GetPrivateField<bool>(component, "_showPlanModal"));
        Assert.True(ReflectionTestHelper.GetPrivateField<bool>(component, "_isEditingPlan"));
        Assert.Equal(plan.Name, planForm.Name);
        Assert.Equal(plan.TimeZoneId, planForm.TimeZoneId);
    }

    [Fact]
    public void GetBalancePointForDate_ReturnsMatchingDailyPoint()
    {
        var points = new List<DailyBalancePoint>
        {
            new(new DateOnly(2026, 3, 21), new Money(100m, "CAD")),
            new(new DateOnly(2026, 3, 22), new Money(125m, "CAD"))
        };

        var match = ReflectionTestHelper.InvokeStatic<DailyBalancePoint?>(typeof(Home), "GetBalancePointForDate", points, new DateOnly(2026, 3, 22));
        var missing = ReflectionTestHelper.InvokeStatic<DailyBalancePoint?>(typeof(Home), "GetBalancePointForDate", points, new DateOnly(2026, 3, 23));

        Assert.NotNull(match);
        Assert.Equal(125m, match.EndOfDayBalance.Amount);
        Assert.Null(missing);
    }

    [Fact]
    public void BuildHealthState_ReturnsHealthyWhenWindowStaysPositive()
    {
        var forecast = new ForecastResult(
            new PredictiveBudget.Domain.Common.DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 27)),
            [],
            new ForecastSummary(
                new Money(120m, "CAD"),
                new DateOnly(2026, 3, 22),
                new Money(185m, "CAD"),
                new DateOnly(2026, 3, 26),
                null),
            [],
            []);

        var state = ReflectionTestHelper.InvokeStatic<object>(typeof(Home), "BuildHealthState", forecast, new DateOnly(2026, 3, 20));

        Assert.Equal("healthy", ReflectionTestHelper.GetPropertyValue<string>(state, "Tone"));
        Assert.Equal("Healthy", ReflectionTestHelper.GetPropertyValue<string>(state, "Badge"));
        Assert.Equal("Window stays above zero", ReflectionTestHelper.GetPropertyValue<string>(state, "Heading"));
    }

    [Fact]
    public void BuildHealthState_ReturnsRiskWhenBelowZeroIsImminent()
    {
        var firstBelowZero = new DateOnly(2026, 3, 25);
        var forecast = new ForecastResult(
            new PredictiveBudget.Domain.Common.DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 4, 5)),
            [],
            new ForecastSummary(
                new Money(-25m, "CAD"),
                firstBelowZero,
                new Money(210m, "CAD"),
                new DateOnly(2026, 3, 20),
                firstBelowZero),
            [firstBelowZero, firstBelowZero.AddDays(1)],
            []);

        var state = ReflectionTestHelper.InvokeStatic<object>(typeof(Home), "BuildHealthState", forecast, new DateOnly(2026, 3, 20));

        Assert.Equal("risk", ReflectionTestHelper.GetPropertyValue<string>(state, "Tone"));
        Assert.Equal("Risk", ReflectionTestHelper.GetPropertyValue<string>(state, "Badge"));
        Assert.Equal("Below zero in 5 days", ReflectionTestHelper.GetPropertyValue<string>(state, "Heading"));
    }

    [Fact]
    public void BuildTransactionListRange_ExtendsTheWindowBackTenDaysAndThroughToday()
    {
        var range = ReflectionTestHelper.InvokeStatic<PredictiveBudget.Domain.Common.DateRange>(
            typeof(Home),
            "BuildTransactionListRange",
            new DateOnly(2026, 3, 25),
            new DateOnly(2026, 4, 20),
            new DateOnly(2026, 3, 20));

        Assert.Equal(new DateOnly(2026, 3, 10), range.Start);
        Assert.Equal(new DateOnly(2026, 4, 20), range.End);
    }

    [Fact]
    public void BuildOccurrenceRows_InsertsCurrentBalanceAfterTodaysTransactions()
    {
        var today = new DateOnly(2026, 3, 22);
        var occurrences = new List<CashflowOccurrence>
        {
            new(today.AddDays(-1), today.AddDays(-1), "Pay", TransactionDirection.Inflow, new Money(50m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid()),
            new(today, today, "Groceries", TransactionDirection.Outflow, new Money(10m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid()),
            new(today, today, "Lunch", TransactionDirection.Outflow, new Money(5m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid()),
            new(today.AddDays(1), today.AddDays(1), "Rent", TransactionDirection.Outflow, new Money(25m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid())
        };

        var forecast = new ForecastResult(
            new PredictiveBudget.Domain.Common.DateRange(today.AddDays(-1), today.AddDays(1)),
            [
                new DailyBalancePoint(today.AddDays(-1), new Money(150m, "CAD")),
                new DailyBalancePoint(today, new Money(135m, "CAD")),
                new DailyBalancePoint(today.AddDays(1), new Money(110m, "CAD"))
            ],
            new ForecastSummary(
                new Money(110m, "CAD"),
                today.AddDays(1),
                new Money(150m, "CAD"),
                today.AddDays(-1),
                null),
            [],
            occurrences);

        var rows = ReflectionTestHelper.InvokeStatic<IReadOnlyList<object>>(typeof(Home), "BuildOccurrenceRows", forecast, today);

        Assert.Equal(["Pay", "Groceries", "Lunch", "Current balance", "Rent"], rows
            .Select(row => ReflectionTestHelper.GetPropertyValue<string>(row, "Name"))
            .ToArray());
        Assert.Equal([150m, 135m, 135m, 135m, 110m], rows
            .Select(row => ReflectionTestHelper.GetPropertyValue<Money>(row, "EndOfDayBalance").Amount)
            .ToArray());
        Assert.True(ReflectionTestHelper.GetPropertyValue<bool>(rows[3], "IsCurrentBalance"));
    }

    [Fact]
    public void BuildOccurrenceRows_AddsCurrentBalanceRowWhenTodayHasNoTransactions()
    {
        var today = new DateOnly(2026, 3, 22);
        var occurrences = new List<CashflowOccurrence>
        {
            new(today.AddDays(-1), today.AddDays(-1), "Pay", TransactionDirection.Inflow, new Money(50m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid()),
            new(today.AddDays(1), today.AddDays(1), "Rent", TransactionDirection.Outflow, new Money(25m, "CAD"), 1, OccurrenceSource.PlannedTransaction, Guid.NewGuid())
        };

        var forecast = new ForecastResult(
            new PredictiveBudget.Domain.Common.DateRange(today.AddDays(-1), today.AddDays(1)),
            [
                new DailyBalancePoint(today.AddDays(-1), new Money(150m, "CAD")),
                new DailyBalancePoint(today, new Money(150m, "CAD")),
                new DailyBalancePoint(today.AddDays(1), new Money(125m, "CAD"))
            ],
            new ForecastSummary(
                new Money(125m, "CAD"),
                today.AddDays(1),
                new Money(150m, "CAD"),
                today.AddDays(-1),
                null),
            [],
            occurrences);

        var rows = ReflectionTestHelper.InvokeStatic<IReadOnlyList<object>>(typeof(Home), "BuildOccurrenceRows", forecast, today);

        Assert.Equal(["Pay", "Current balance", "Rent"], rows
            .Select(row => ReflectionTestHelper.GetPropertyValue<string>(row, "Name"))
            .ToArray());
        Assert.True(ReflectionTestHelper.GetPropertyValue<bool>(rows[1], "IsCurrentBalance"));
    }

    private static Home CreateComponent(BudgetPlanService service)
    {
        var component = new Home();
        ReflectionTestHelper.SetPrivateProperty(component, "BudgetPlanService", service);
        ReflectionTestHelper.SetPrivateProperty(component, "Snackbar", Substitute.For<ISnackbar>());
        return component;
    }
}
