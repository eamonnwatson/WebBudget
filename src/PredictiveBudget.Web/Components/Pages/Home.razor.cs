using Microsoft.AspNetCore.Components;
using MudBlazor;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Web.Components.Pages.Models;

namespace PredictiveBudget.Web.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<BudgetPlan> _plans = [];
    private CreateBudgetPlanFormModel _createForm = CreateBudgetPlanFormModel.CreateDefault();
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
        => await LoadPlansAsync();

    private async Task LoadPlansAsync()
    {
        _isLoading = true;
        _plans.Clear();
        _plans.AddRange(await BudgetPlanService.ListAsync(CancellationToken.None));
        _isLoading = false;
    }

    private async Task CreatePlanAsync()
    {
        var plan = await BudgetPlanService.CreateAsync(
            new CreateBudgetPlanRequest(
                _createForm.Name,
                _createForm.Currency,
                _createForm.StartingBalance ?? 0m,
                ToDateOnly(_createForm.BalanceAsOfDate),
                _createForm.TimeZoneId),
            CancellationToken.None);

        Snackbar.Add($"Created plan '{plan.Name}'.", Severity.Success);
        _createForm = CreateBudgetPlanFormModel.CreateDefault();
        await LoadPlansAsync();
        Navigation.NavigateTo($"/plans/{plan.PlanId}");
    }

    private static DateOnly ToDateOnly(DateTime? value)
        => DateOnly.FromDateTime(value ?? DateTime.Today);

    private static string FormatMoney(Money money)
        => $"{money.Amount:N2} {money.Currency}";

    private static string FormatDate(DateOnly date)
        => date.ToString("MMM d, yyyy");
}
