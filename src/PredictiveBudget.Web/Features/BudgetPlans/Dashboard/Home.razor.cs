using Microsoft.AspNetCore.Components;
using MudBlazor;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Web.Features.BudgetPlans.Models;

namespace PredictiveBudget.Web.Features.BudgetPlans.Dashboard;

/// <summary>
/// Backs the dashboard experience for selecting plans, running forecasts, and managing quick actions.
/// </summary>
public partial class Home : ComponentBase
{
    private const int RecentTransactionHistoryDays = 10;

    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<BudgetPlan> _plans = [];

    private BalanceUpdateFormModel _balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private CreateBudgetPlanFormModel _planForm = CreateBudgetPlanFormModel.CreateDefault();
    private ForecastFormModel _forecastForm = ForecastFormModel.CreateDefault();

    private BudgetPlan? _selectedPlan;
    private ForecastResult? _forecastResult;
    private ForecastResult? _transactionForecastResult;
    private Guid? _deletePlanId;
    private string _deletePlanName = string.Empty;
    private Guid? _editingPlanId;
    private Guid? _selectedPlanId;
    private bool _isEditingPlan;
    private bool _isLoading = true;
    private bool _showDeletePlanModal;
    private bool _showPlanModal;
    private OccurrenceOverrideFormModel _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private Guid? _editingOverrideId;
    private bool _showOverrideModal;
    private string _overrideModalDescription = string.Empty;
    private bool _mobileDrawerOpen;

    protected override async Task OnInitializedAsync()
        => await LoadPlansAsync(resetForecastWindow: true);

    private static DateOnly Today
        => DateOnly.FromDateTime(DateTime.Today);

    private IReadOnlyList<ForecastOccurrenceRow> TransactionRows
        => BuildOccurrenceRows(_transactionForecastResult, Today);

    private int ForecastWindowDayCount
        => _forecastResult is null
            ? 0
            : (_forecastResult.Range.End.DayNumber - _forecastResult.Range.Start.DayNumber) + 1;

    private string ForecastWindowLabel
        => _forecastForm.StartDate is null || _forecastForm.EndDate is null
            ? "Select a forecast window"
            : $"{_forecastForm.StartDate.Value:MMM d} - {_forecastForm.EndDate.Value:MMM d, yyyy}";

    private DashboardHealthState HealthState
        => BuildHealthState(_forecastResult, Today);

    private string DeletePlanMessage
        => $"Delete '{_deletePlanName}'? This removes the plan and its associated rules, transactions, and overrides.";

    private string HealthTone
        => HealthState.Tone switch
        {
            "healthy" => "success",
            "watch" => "warning",
            "risk" => "danger",
            _ => "neutral"
        };

    private string PlanModalDescription
        => _isEditingPlan
            ? "Update the plan name, reconcile the balance checkpoint, or change the time zone for this forecast."
            : "Create another plan with its own starting balance, currency, and time zone.";

    private string PlanModalKicker
        => _isEditingPlan ? "Plan settings" : "New plan";

    private string PlanModalSubmitText
        => _isEditingPlan ? "Save changes" : "Create plan";

    private string PlanModalTitle
        => _isEditingPlan ? "Edit budget plan" : "Create a new budget plan";

    private DailyBalancePoint? TodayBalancePoint
        => GetBalancePointForDate(_forecastResult?.DailyPoints ?? [], Today);

    private string WorkspaceHref
        => _selectedPlan is null ? "/" : $"/plans/{_selectedPlan.PlanId}";

    private async Task ChangeSelectedPlanAsync(Guid planId)
        => await LoadPlansAsync(planId, resetForecastWindow: true);

    private void CloseAllModals()
    {
        _showPlanModal = false;
        _showDeletePlanModal = false;
        _deletePlanId = null;
        _deletePlanName = string.Empty;
        _editingPlanId = null;
        _isEditingPlan = false;
        _showOverrideModal = false;
        _editingOverrideId = null;
    }

    private void ToggleMobileDrawer()
        => _mobileDrawerOpen = !_mobileDrawerOpen;

    private string OverrideModalTitle
        => _editingOverrideId.HasValue ? "Edit occurrence override" : "Add occurrence override";

    private OccurrenceOverride? FindExistingOverride(ForecastOccurrenceRow row)
    {
        if (_selectedPlan is null || row.Source is null || row.SourceId is null)
        {
            return null;
        }

        return _selectedPlan.Overrides.FirstOrDefault(o =>
            o.Source == row.Source.Value &&
            o.SourceId == row.SourceId.Value &&
            o.OriginalDate == row.OriginalDate);
    }

    private void OpenOverrideModal(ForecastOccurrenceRow row)
    {
        if (_selectedPlan is null || row.Source is null || row.SourceId is null)
        {
            return;
        }

        CloseAllModals();

        var existing = FindExistingOverride(row);
        _overrideModalDescription = $"{row.Name} on {FormatDate(row.OriginalDate)}";

        if (existing is not null)
        {
            _editingOverrideId = existing.OverrideId;
            _overrideForm = new OccurrenceOverrideFormModel
            {
                Source = existing.Source,
                SourceId = existing.SourceId.ToString(),
                OriginalDate = existing.OriginalDate.ToDateTime(TimeOnly.MinValue),
                Action = existing.Action,
                NewDate = existing.NewDate?.ToDateTime(TimeOnly.MinValue),
                NewAmount = existing.NewAmount?.Amount,
                NewName = existing.NewName
            };
        }
        else
        {
            _editingOverrideId = null;
            _overrideForm = new OccurrenceOverrideFormModel
            {
                Source = row.Source.Value,
                SourceId = row.SourceId.Value.ToString(),
                OriginalDate = row.OriginalDate.ToDateTime(TimeOnly.MinValue),
                Action = OverrideAction.Skip
            };
        }

        _showOverrideModal = true;
    }

    private async Task SaveOverrideAsync()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        if (_editingOverrideId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateOverrideAsync(
                _selectedPlan.PlanId,
                _editingOverrideId.Value,
                new UpdateOccurrenceOverrideRequest(
                    _overrideForm.Source,
                    Guid.Parse(_overrideForm.SourceId),
                    ToDateOnly(_overrideForm.OriginalDate),
                    _overrideForm.Action,
                    _overrideForm.NewDate is null ? null : ToDateOnly(_overrideForm.NewDate),
                    _overrideForm.NewAmount,
                    _overrideForm.NewName),
                CancellationToken.None);

            Snackbar.Add("Occurrence override updated.", Severity.Success);
        }
        else
        {
            updatedPlan = await BudgetPlanService.AddOverrideAsync(
                _selectedPlan.PlanId,
                new AddOccurrenceOverrideRequest(
                    _overrideForm.Source,
                    Guid.Parse(_overrideForm.SourceId),
                    ToDateOnly(_overrideForm.OriginalDate),
                    _overrideForm.Action,
                    _overrideForm.NewDate is null ? null : ToDateOnly(_overrideForm.NewDate),
                    _overrideForm.NewAmount,
                    _overrideForm.NewName),
                CancellationToken.None);

            Snackbar.Add("Occurrence override added.", Severity.Success);
        }

        int updatedIndex = _plans.FindIndex(p => p.PlanId == updatedPlan.PlanId);
        if (updatedIndex >= 0)
        {
            _plans[updatedIndex] = updatedPlan;
        }

        _selectedPlan = updatedPlan;
        CloseAllModals();
        await RunForecastAsync(showSnackbar: false);
    }

    private async Task CreatePlanAsync()
    {
        var plan = await BudgetPlanService.CreateAsync(
            new CreateBudgetPlanRequest(
                _planForm.Name,
                _planForm.Currency,
                _planForm.StartingBalance ?? 0m,
                ToDateOnly(_planForm.BalanceAsOfDate),
                _planForm.TimeZoneId),
            CancellationToken.None);

        CloseAllModals();
        _planForm = CreateBudgetPlanFormModel.CreateDefault();
        Snackbar.Add($"Created plan '{plan.Name}'.", Severity.Success);
        await LoadPlansAsync(plan.PlanId, resetForecastWindow: true);
    }

    private async Task DeletePlanAsync()
    {
        if (!_deletePlanId.HasValue)
        {
            return;
        }

        var deletedPlanId = _deletePlanId.Value;
        var deletedPlanName = _deletePlanName;

        await BudgetPlanService.DeleteAsync(deletedPlanId, CancellationToken.None);

        CloseAllModals();
        Snackbar.Add($"Deleted plan '{deletedPlanName}'.", Severity.Success);

        bool deletedSelectedPlan = _selectedPlanId == deletedPlanId;
        await LoadPlansAsync(
            preferredPlanId: deletedSelectedPlan ? null : _selectedPlanId,
            resetForecastWindow: deletedSelectedPlan);
    }

    private static string FormatDate(DateOnly date)
        => date.ToString("MMM d, yyyy");

    private static string FormatMoney(Money money)
        => $"{money.Amount:N2} {money.Currency}";

    private static string FormatSignedMoney(TransactionDirection direction, Money money)
    {
        string sign = direction == TransactionDirection.Outflow ? "-" : "+";
        return $"{sign}{money.Amount:N2} {money.Currency}";
    }

    private static DailyBalancePoint? GetBalancePointForDate(IReadOnlyList<DailyBalancePoint> points, DateOnly date)
        => points.FirstOrDefault(point => point.Date == date);

    private static Color GetHealthColor(string tone)
        => tone switch
        {
            "healthy" => Color.Success,
            "watch" => Color.Warning,
            "risk" => Color.Error,
            _ => Color.Info
        };

    private static string GetOccurrenceSourceLabel(CashflowOccurrence occurrence)
        => occurrence.Source switch
        {
            OccurrenceSource.RecurringRule => "Recurring rule",
            OccurrenceSource.PlannedTransaction => "Planned transaction",
            _ => occurrence.Source.ToString()
        };

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
                _transactionForecastResult = null;
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
                ResetForecastWindow();
            }

            await RunForecastAsync(showSnackbar: false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenCreatePlanModal()
    {
        CloseAllModals();
        _isEditingPlan = false;
        _planForm = CreateBudgetPlanFormModel.CreateDefault();
        _showPlanModal = true;
    }

    private void OpenDeletePlanModal()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        CloseAllModals();
        _deletePlanId = _selectedPlan.PlanId;
        _deletePlanName = _selectedPlan.Name;
        _showDeletePlanModal = true;
    }

    private void OpenEditPlanModal()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        CloseAllModals();
        _editingPlanId = _selectedPlan.PlanId;
        _isEditingPlan = true;
        _planForm = CreateBudgetPlanFormModel.CreateFromPlan(_selectedPlan);
        _showPlanModal = true;
    }

    private void ResetForecastWindow()
        => _forecastForm = ForecastFormModel.CreateDefault(durationDays: 365);

    private async Task RunForecastAsync()
        => await RunForecastAsync(showSnackbar: true);

    private async Task RunForecastAsync(bool showSnackbar)
    {
        if (_selectedPlan is null)
        {
            _forecastResult = null;
            _transactionForecastResult = null;
            return;
        }

        var forecastStart = ToDateOnly(_forecastForm.StartDate);
        var forecastEnd = ToDateOnly(_forecastForm.EndDate);

        _forecastResult = await BudgetPlanService.ForecastAsync(
            _selectedPlan.PlanId,
            new ForecastRequest(
                forecastStart,
                forecastEnd),
            CancellationToken.None);

        var transactionListRange = BuildTransactionListRange(forecastStart, forecastEnd, Today);
        _transactionForecastResult = transactionListRange.Start == forecastStart && transactionListRange.End == forecastEnd
            ? _forecastResult
            : await BudgetPlanService.ForecastAsync(
                _selectedPlan.PlanId,
                new ForecastRequest(transactionListRange.Start, transactionListRange.End),
                CancellationToken.None);

        if (showSnackbar)
        {
            Snackbar.Add("Forecast calculated.", Severity.Success);
        }
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

    private async Task SavePlanAsync()
    {
        if (_isEditingPlan)
        {
            await UpdatePlanAsync();
            return;
        }

        await CreatePlanAsync();
    }

    private static DateOnly ToDateOnly(DateTime? value)
        => DateOnly.FromDateTime(value ?? DateTime.Today);

    private async Task UpdatePlanAsync()
    {
        if (!_editingPlanId.HasValue)
        {
            return;
        }

        var updatedPlan = await BudgetPlanService.UpdateAsync(
            _editingPlanId.Value,
            new UpdateBudgetPlanRequest(
                _planForm.Name,
                _planForm.StartingBalance ?? 0m,
                ToDateOnly(_planForm.BalanceAsOfDate),
                _planForm.TimeZoneId),
            CancellationToken.None);

        CloseAllModals();
        Snackbar.Add($"Updated plan '{updatedPlan.Name}'.", Severity.Success);
        await LoadPlansAsync(updatedPlan.PlanId, resetForecastWindow: false);
    }

    private static PredictiveBudget.Domain.Common.DateRange BuildTransactionListRange(DateOnly forecastStart, DateOnly forecastEnd, DateOnly today)
    {
        var historyStart = today.AddDays(-RecentTransactionHistoryDays);
        var start = forecastStart < historyStart ? forecastStart : historyStart;
        var end = forecastEnd > today ? forecastEnd : today;
        return new PredictiveBudget.Domain.Common.DateRange(start, end);
    }

    private static IReadOnlyList<ForecastOccurrenceRow> BuildOccurrenceRows(ForecastResult? result, DateOnly today)
    {
        if (result is null || result.DailyPoints.Count == 0)
        {
            return [];
        }

        var dailyBalances = result.DailyPoints.ToDictionary(point => point.Date, point => point.EndOfDayBalance);
        var rows = result.Occurrences
            .Select(occurrence => new ForecastOccurrenceRow(
                occurrence.Date,
                occurrence.OriginalDate,
                occurrence.Name,
                GetOccurrenceSourceLabel(occurrence),
                occurrence.Direction,
                occurrence.Amount,
                dailyBalances[occurrence.Date],
                false,
                occurrence.Source,
                occurrence.SourceId))
            .ToList();

        if (!dailyBalances.TryGetValue(today, out var todayBalance))
        {
            return rows;
        }

        var currentBalanceRow = new ForecastOccurrenceRow(
            today,
            today,
            "Current balance",
            "Live checkpoint",
            null,
            null,
            todayBalance,
            true,
            null,
            null);

        int lastTodayIndex = rows.FindLastIndex(row => row.Date == today);
        if (lastTodayIndex >= 0)
        {
            rows.Insert(lastTodayIndex + 1, currentBalanceRow);
            return rows;
        }

        int firstFutureIndex = rows.FindIndex(row => row.Date > today);
        if (firstFutureIndex >= 0)
        {
            rows.Insert(firstFutureIndex, currentBalanceRow);
            return rows;
        }

        rows.Add(currentBalanceRow);
        return rows;
    }

    private static string FormatRowAmount(ForecastOccurrenceRow row)
        => row.Direction is null || row.Amount is null
            ? "Current"
            : FormatSignedMoney(row.Direction.Value, row.Amount.Value);

    private static string GetAmountClass(ForecastOccurrenceRow row)
    {
        if (row.IsCurrentBalance)
        {
            return "transaction-current-amount";
        }

        return row.Direction == TransactionDirection.Outflow ? "amount-negative" : "amount-positive";
    }

    private static string GetNameClass(ForecastOccurrenceRow row)
        => row.IsCurrentBalance ? "transaction-current-label" : string.Empty;

    private static string GetSourceClass(ForecastOccurrenceRow row)
        => row.IsCurrentBalance ? "transaction-current-source" : string.Empty;

    private static string GetBalanceClass(ForecastOccurrenceRow row)
        => row.IsCurrentBalance
            ? "transaction-running-balance transaction-current-balance"
            : "transaction-running-balance";

    private static string GetCellClass(ForecastOccurrenceRow row, string? baseClass = null)
    {
        if (!row.IsCurrentBalance)
        {
            return baseClass ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(baseClass)
            ? "transaction-row-current"
            : $"{baseClass} transaction-row-current";
    }

    private static DashboardHealthState BuildHealthState(ForecastResult? result, DateOnly today)
    {
        if (result is null)
        {
            return new DashboardHealthState(
                "neutral",
                "Forecast",
                "Run a forecast",
                "Select a window to populate the balance trend and risk outlook.");
        }

        if (result.BelowZeroDates.Count == 0)
        {
            return new DashboardHealthState(
                "healthy",
                "Healthy",
                "Window stays above zero",
                "No below-zero days appear in this forecast range.");
        }

        var firstBelowZeroDate = result.Summary.FirstBelowZeroDate ?? result.BelowZeroDates[0];
        var belowZeroCopy = BuildBelowZeroCopy(result.BelowZeroDates.Count);
        int daysUntil = firstBelowZeroDate.DayNumber - today.DayNumber;

        if (daysUntil <= 0)
        {
            return new DashboardHealthState(
                "risk",
                "Risk",
                "Below zero now",
                $"{belowZeroCopy} Reconcile the balance or adjust upcoming outflows.");
        }

        if (daysUntil <= 14)
        {
            return new DashboardHealthState(
                "risk",
                "Risk",
                $"Below zero in {FormatDayCount(daysUntil)}",
                $"{belowZeroCopy} Review the next two weeks closely.");
        }

        return new DashboardHealthState(
            "watch",
            "Watch",
            $"Below zero in {FormatDayCount(daysUntil)}",
            $"{belowZeroCopy} Keep an eye on the current forecast window.");
    }

    private static string BuildBelowZeroCopy(int count)
        => count == 1
            ? "1 day dips below zero in this window."
            : $"{count} days dip below zero in this window.";

    private static string FormatDayCount(int days)
        => days == 1 ? "1 day" : $"{days} days";

    private sealed record ForecastOccurrenceRow(
        DateOnly Date,
        DateOnly OriginalDate,
        string Name,
        string SourceLabel,
        TransactionDirection? Direction,
        Money? Amount,
        Money EndOfDayBalance,
        bool IsCurrentBalance,
        OccurrenceSource? Source,
        Guid? SourceId);

    private sealed record DashboardHealthState(string Tone, string Badge, string Heading, string Detail);
}
