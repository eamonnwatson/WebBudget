using Microsoft.AspNetCore.Components;
using MudBlazor;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Web.Components.Pages.Models;

namespace PredictiveBudget.Web.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<BudgetPlan> _plans = [];
    private List<ChartSeries<double>> _forecastChartSeries = [];
    private string[] _forecastChartLabels = [];
    private LineChartOptions _forecastChartOptions = CreateForecastChartOptions("CAD");
    private CreateBudgetPlanFormModel _createForm = CreateBudgetPlanFormModel.CreateDefault();
    private BalanceUpdateFormModel _balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private ForecastFormModel _forecastForm = ForecastFormModel.CreateDefault();

    private BudgetPlan? _selectedPlan;
    private ForecastResult? _forecastResult;
    private Guid? _selectedPlanId;
    private bool _isLoading = true;
    private bool _showCreatePlanModal;

    protected override async Task OnInitializedAsync()
        => await LoadPlansAsync(resetForecastWindow: true);

    private IReadOnlyList<CashflowOccurrence> UpcomingForecastOccurrences
        => _forecastResult?.Occurrences ?? [];

    private async Task LoadPlansAsync(Guid? preferredPlanId = null, bool resetForecastWindow = false)
    {
        _isLoading = true;

        try
        {
            var previousSelection = _selectedPlanId;

            _plans.Clear();
            _plans.AddRange(await BudgetPlanService.ListAsync(CancellationToken.None));

            if (_plans.Count == 0)
            {
                _selectedPlan = null;
                _selectedPlanId = null;
                _forecastResult = null;
                ClearForecastChart();
                CloseAllModals();
                return;
            }

            var targetPlanId = preferredPlanId ?? previousSelection;
            var selectedPlan = targetPlanId.HasValue
                ? _plans.FirstOrDefault(plan => plan.PlanId == targetPlanId.Value)
                : null;

            var resolvedPlan = selectedPlan ?? _plans[0];
            bool selectionChanged = _selectedPlanId != resolvedPlan.PlanId;

            _selectedPlan = resolvedPlan;
            _selectedPlanId = resolvedPlan.PlanId;
            _balanceForm = BalanceUpdateFormModel.CreateDefault(resolvedPlan.StartingBalance.Amount, resolvedPlan.BalanceAsOfDate);

            if (resetForecastWindow || selectionChanged || _forecastForm.StartDate is null || _forecastForm.EndDate is null)
            {
                ResetForecastWindow(resolvedPlan);
            }

            await RunForecastAsync(showSnackbar: false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ChangeSelectedPlanAsync(Guid planId)
        => await LoadPlansAsync(planId, resetForecastWindow: true);

    private void OpenCreatePlanModal()
    {
        CloseAllModals();
        _createForm = CreateBudgetPlanFormModel.CreateDefault();
        _showCreatePlanModal = true;
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

        CloseAllModals();
        Snackbar.Add($"Created plan '{plan.Name}'.", Severity.Success);
        _createForm = CreateBudgetPlanFormModel.CreateDefault();
        await LoadPlansAsync(plan.PlanId, resetForecastWindow: true);
    }

    private async Task SaveBalanceAsync()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        var updatedPlan = await BudgetPlanService.UpdateStartingBalanceAsync(
            _selectedPlan.PlanId,
            new UpdateStartingBalanceRequest(
                _balanceForm.Amount ?? 0m,
                ToDateOnly(_balanceForm.BalanceAsOfDate)),
            CancellationToken.None);

        Snackbar.Add("Balance checkpoint updated.", Severity.Success);
        await LoadPlansAsync(updatedPlan.PlanId, resetForecastWindow: false);
    }

    private async Task RunForecastAsync()
        => await RunForecastAsync(showSnackbar: true);

    private async Task RunForecastAsync(bool showSnackbar)
    {
        if (_selectedPlan is null)
        {
            _forecastResult = null;
            ClearForecastChart();
            return;
        }

        _forecastResult = await BudgetPlanService.ForecastAsync(
            _selectedPlan.PlanId,
            new ForecastRequest(
                ToDateOnly(_forecastForm.StartDate),
                ToDateOnly(_forecastForm.EndDate)),
            CancellationToken.None);

        UpdateForecastChart();

        if (showSnackbar)
        {
            Snackbar.Add("Forecast calculated.", Severity.Success);
        }
    }

    private static string FormatMoney(Money money)
        => $"{money.Amount:N2} {money.Currency}";

    private static string FormatSignedMoney(TransactionDirection direction, Money money)
    {
        string sign = direction == TransactionDirection.Outflow ? "-" : "+";
        return $"{sign}{money.Amount:N2} {money.Currency}";
    }

    private static string FormatDate(DateOnly date)
        => date.ToString("MMM d, yyyy");

    private static DateOnly ToDateOnly(DateTime? value)
        => DateOnly.FromDateTime(value ?? DateTime.Today);

    private static string GetOccurrenceSourceLabel(CashflowOccurrence occurrence)
        => occurrence.Source switch
        {
            OccurrenceSource.RecurringRule => "Recurring rule",
            OccurrenceSource.PlannedTransaction => "Planned transaction",
            _ => occurrence.Source.ToString()
        };

    private static string GetAmountClass(TransactionDirection direction)
        => direction == TransactionDirection.Outflow ? "amount-negative" : "amount-positive";

    private void CloseAllModals()
        => _showCreatePlanModal = false;

    private void ResetForecastWindow(BudgetPlan plan)
        => _forecastForm = ForecastFormModel.CreateDefault(plan.BalanceAsOfDate, durationDays: 365);

    private void UpdateForecastChart()
    {
        if (_forecastResult is null || _forecastResult.DailyPoints.Count == 0 || _selectedPlan is null)
        {
            ClearForecastChart();
            return;
        }

        _forecastChartSeries =
        [
            new ChartSeries<double>
            {
                Name = $"{_selectedPlan.Name} balance",
                Data = _forecastResult.DailyPoints
                    .Select(point => (double)point.EndOfDayBalance.Amount)
                    .ToArray()
            }
        ];

        _forecastChartLabels = BuildForecastChartLabels(_forecastResult.DailyPoints);
        _forecastChartOptions = CreateForecastChartOptions(_selectedPlan.Currency);
    }

    private void ClearForecastChart()
    {
        _forecastChartSeries = [];
        _forecastChartLabels = [];
        _forecastChartOptions = CreateForecastChartOptions(_selectedPlan?.Currency ?? "CAD");
    }

    private static string[] BuildForecastChartLabels(IReadOnlyList<DailyBalancePoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var labels = new string[points.Count];

        for (int index = 0; index < points.Count; index++)
        {
            bool isFirst = index == 0;
            bool isLast = index == points.Count - 1;
            bool isMonthBoundary = index > 0 && points[index].Date.Month != points[index - 1].Date.Month;

            labels[index] = isFirst || isLast || isMonthBoundary
                ? points[index].Date.ToString("MMM d")
                : string.Empty;
        }

        return labels;
    }

    private static LineChartOptions CreateForecastChartOptions(string currency)
        => new()
        {
            ShowLegend = false,
            ShowDataMarkers = false,
            LineStrokeWidth = 3,
            YAxisRequireZeroPoint = false,
            YAxisToStringFunc = value => FormatChartAxisValue(value, currency)
        };

    private static string FormatChartAxisValue(double value, string currency)
        => $"{value:N0} {currency}";
}
