using MudBlazor;
using NSubstitute;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Web.Components.Pages;
using PredictiveBudget.Web.Components.Pages.Models;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

public sealed class HomeTests
{
    [Fact]
    public async Task OnInitializedAsync_LoadsPlansIntoComponentState()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = new Home();
        ReflectionTestHelper.SetPrivateProperty(component, "BudgetPlanService", service);

        await ReflectionTestHelper.InvokeAsync(component, "OnInitializedAsync");

        var plans = ReflectionTestHelper.GetPrivateField<List<BudgetPlan>>(component, "_plans");
        Assert.Single(plans);
        Assert.False(ReflectionTestHelper.GetPrivateField<bool>(component, "_isLoading"));
    }

    [Fact]
    public async Task CreatePlanAsync_CreatesPlanResetsFormAndNavigatesToDetails()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var navigation = new TestNavigationManager();
        var component = new Home();
        ReflectionTestHelper.SetPrivateProperty(component, "BudgetPlanService", service);
        ReflectionTestHelper.SetPrivateProperty(component, "Navigation", navigation);
        ReflectionTestHelper.SetPrivateProperty(component, "Snackbar", Substitute.For<ISnackbar>());
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
        Assert.Equal("USD", createdPlan.Currency);
        Assert.EndsWith($"/plans/{createdPlan.PlanId}", navigation.Uri, StringComparison.Ordinal);
        var resetForm = ReflectionTestHelper.GetPrivateField<CreateBudgetPlanFormModel>(component, "_createForm");
        Assert.Equal("CAD", resetForm.Currency);
        Assert.Equal(string.Empty, resetForm.Name);
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
}
