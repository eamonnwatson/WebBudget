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

        Assert.Equal(2, plans.Count);
        Assert.Equal(expectedPlan.PlanId, selectedPlan.PlanId);
        Assert.Equal(expectedPlan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue), forecastForm.StartDate);
        Assert.Equal(expectedPlan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue).AddDays(365), forecastForm.EndDate);
        Assert.Equal(expectedPlan.BalanceAsOfDate, forecastResult.Range.Start);
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
        Assert.Equal(new DateTime(2026, 4, 10), forecastForm.StartDate);
        Assert.Equal(new DateTime(2027, 4, 10), forecastForm.EndDate);
    }

    [Fact]
    public async Task CreatePlanAsync_CreatesPlanResetsFormAndSelectsNewPlan()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var component = CreateComponent(service);
        ReflectionTestHelper.SetPrivateField(component, "_createForm", new CreateBudgetPlanFormModel
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
        var resetForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_createForm");

        Assert.Equal("USD", createdPlan.Currency);
        Assert.Equal(createdPlan.PlanId, selectedPlan.PlanId);
        Assert.Equal(createdPlan.BalanceAsOfDate, forecastResult.Range.Start);
        Assert.Equal("CAD", resetForm.Currency);
        Assert.Equal(string.Empty, resetForm.Name);
    }

    [Fact]
    public void OpenCreatePlanModal_ResetsFormAndShowsCreateModal()
    {
        var component = CreateComponent(new WebBudgetPlanContext().CreateService());
        ReflectionTestHelper.SetPrivateField(component, "_createForm", new CreateBudgetPlanFormModel
        {
            Name = "Existing",
            Currency = "USD",
            StartingBalance = 99m,
            BalanceAsOfDate = new DateTime(2026, 3, 22),
            TimeZoneId = "America/New_York"
        });

        ReflectionTestHelper.InvokeVoid(component, "OpenCreatePlanModal");

        var resetForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_createForm");

        Assert.True(ReflectionTestHelper.GetPrivateField<bool>(component, "_showCreatePlanModal"));
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
    public async Task BuildRunningBalances_TracksBalanceAfterEachOccurrence()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var occurrences = new List<CashflowOccurrence>
        {
            new(new DateOnly(2026, 3, 21), "Pay", TransactionDirection.Inflow, new Money(50m, "CAD"), OccurrenceSource.PlannedTransaction, Guid.NewGuid()),
            new(new DateOnly(2026, 3, 22), "Rent", TransactionDirection.Outflow, new Money(25m, "CAD"), OccurrenceSource.PlannedTransaction, Guid.NewGuid())
        };

        var balances = ReflectionTestHelper.InvokeStatic<IReadOnlyList<Money>>(typeof(Home), "BuildRunningBalances", plan, occurrences);

        Assert.Equal([150m, 125m], balances.Select(balance => balance.Amount).ToArray());
    }

    private static Home CreateComponent(BudgetPlanService service)
    {
        var component = new Home();
        ReflectionTestHelper.SetPrivateProperty(component, "BudgetPlanService", service);
        ReflectionTestHelper.SetPrivateProperty(component, "Snackbar", Substitute.For<ISnackbar>());
        return component;
    }
}
