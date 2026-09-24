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

    private readonly List<BudgetPlan> plans = [];

    private BalanceUpdateFormModel balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private CreateBudgetPlanFormModel planForm = CreateBudgetPlanFormModel.CreateDefault();
    private ForecastFormModel forecastForm = ForecastFormModel.CreateDefault();

    private BudgetPlan? selectedPlan;
    private ForecastResult? forecastResult;
    private ForecastResult? transactionForecastResult;
    private Guid? deletePlanId;
    private string deletePlanName = string.Empty;
    private Guid? editingPlanId;
    private Guid? selectedPlanId;
    private bool isEditingPlan;
    private bool isLoading = true;
    private bool showDeletePlanModal;
    private bool showPlanModal;
    private OccurrenceOverrideFormModel overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private Guid? editingOverrideId;
    private bool showOverrideModal;
    private string overrideModalDescription = string.Empty;
    private bool mobileDrawerOpen;

    protected override async Task OnInitializedAsync()
        => await LoadPlansAsync(resetForecastWindow: true);

    private static DateOnly Today
        => DateOnly.FromDateTime(DateTime.Today);

    private IReadOnlyList<ForecastOccurrenceRow> TransactionRows
        => BuildOccurrenceRows(transactionForecastResult, Today);

    private int ForecastWindowDayCount
        => forecastResult is null
            ? 0
            : (forecastResult.Range.End.DayNumber - forecastResult.Range.Start.DayNumber) + 1;

    private string ForecastWindowLabel
        => forecastForm.StartDate is null || forecastForm.EndDate is null
            ? "Select a forecast window"
            : $"{forecastForm.StartDate.Value:MMM d} - {forecastForm.EndDate.Value:MMM d, yyyy}";

    private DashboardHealthState HealthState
        => BuildHealthState(forecastResult, Today);

    private string DeletePlanMessage
        => $"Delete '{deletePlanName}'? This removes the plan and its associated rules, transactions, and overrides.";

    private string HealthTone
        => HealthState.Tone switch
        {
            "healthy" => "success",
            "watch" => "warning",
            "risk" => "danger",
            _ => "neutral"
        };

    private string PlanModalDescription
        => isEditingPlan
            ? "Update the plan name, reconcile the balance checkpoint, or change the time zone for this forecast."
            : "Create another plan with its own starting balance, currency, and time zone.";

    private string PlanModalKicker
        => isEditingPlan ? "Plan settings" : "New plan";

    private string PlanModalSubmitText
        => isEditingPlan ? "Save changes" : "Create plan";

    private string PlanModalTitle
        => isEditingPlan ? "Edit budget plan" : "Create a new budget plan";

    private DailyBalancePoint? TodayBalancePoint
        => GetBalancePointForDate(forecastResult?.DailyPoints ?? [], Today);

    private string WorkspaceHref
        => selectedPlan is null ? "/" : $"/plans/{selectedPlan.PlanId}";

    private async Task ChangeSelectedPlanAsync(Guid planId)
        => await LoadPlansAsync(planId, resetForecastWindow: true);

    private void CloseAllModals()
    {
        showPlanModal = false;
        showDeletePlanModal = false;
        deletePlanId = null;
        deletePlanName = string.Empty;
        editingPlanId = null;
        isEditingPlan = false;
        showOverrideModal = false;
        editingOverrideId = null;
    }

    private void ToggleMobileDrawer()
        => mobileDrawerOpen = !mobileDrawerOpen;

    private string OverrideModalTitle
        => editingOverrideId.HasValue ? "Edit occurrence override" : "Add occurrence override";

    private OccurrenceOverride? FindExistingOverride(ForecastOccurrenceRow row)
    {
        if (selectedPlan is null || row.Source is null || row.SourceId is null)
        {
            return null;
        }

        return selectedPlan.Overrides.FirstOrDefault(o =>
            o.Source == row.Source.Value &&
            o.SourceId == row.SourceId.Value &&
            o.OriginalDate == row.OriginalDate);
    }

    private void OpenOverrideModal(ForecastOccurrenceRow row)
    {
        if (selectedPlan is null || row.Source is null || row.SourceId is null)
        {
            return;
        }

        CloseAllModals();

        var existing = FindExistingOverride(row);
        overrideModalDescription = $"{row.Name} on {FormatDate(row.OriginalDate)}";

        if (existing is not null)
        {
            editingOverrideId = existing.OverrideId;
            overrideForm = new OccurrenceOverrideFormModel
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
            editingOverrideId = null;
            overrideForm = new OccurrenceOverrideFormModel
            {
                Source = row.Source.Value,
                SourceId = row.SourceId.Value.ToString(),
                OriginalDate = row.OriginalDate.ToDateTime(TimeOnly.MinValue),
                Action = OverrideAction.Skip
            };
        }

        showOverrideModal = true;
    }

    private async Task SaveOverrideAsync()
    {
        if (selectedPlan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        if (editingOverrideId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateOverrideAsync(
                selectedPlan.PlanId,
                editingOverrideId.Value,
                new UpdateOccurrenceOverrideRequest(
                    overrideForm.Source,
                    Guid.Parse(overrideForm.SourceId),
                    ToDateOnly(overrideForm.OriginalDate),
                    overrideForm.Action,
                    overrideForm.NewDate is null ? null : ToDateOnly(overrideForm.NewDate),
                    overrideForm.NewAmount,
                    overrideForm.NewName),
                CancellationToken.None);

            Snackbar.Add("Occurrence override updated.", Severity.Success);
        }
        else
        {
            updatedPlan = await BudgetPlanService.AddOverrideAsync(
                selectedPlan.PlanId,
                new AddOccurrenceOverrideRequest(
                    overrideForm.Source,
                    Guid.Parse(overrideForm.SourceId),
                    ToDateOnly(overrideForm.OriginalDate),
                    overrideForm.Action,
                    overrideForm.NewDate is null ? null : ToDateOnly(overrideForm.NewDate),
                    overrideForm.NewAmount,
                    overrideForm.NewName),
                CancellationToken.None);

            Snackbar.Add("Occurrence override added.", Severity.Success);
        }

        int updatedIndex = plans.FindIndex(p => p.PlanId == updatedPlan.PlanId);
        if (updatedIndex >= 0)
        {
            plans[updatedIndex] = updatedPlan;
        }

        selectedPlan = updatedPlan;
        CloseAllModals();
        await RunForecastAsync(showSnackbar: false);
    }

    private async Task CreatePlanAsync()
    {
        var plan = await BudgetPlanService.CreateAsync(
            new CreateBudgetPlanRequest(
                planForm.Name,
                planForm.Currency,
                planForm.StartingBalance ?? 0m,
                ToDateOnly(planForm.BalanceAsOfDate),
                planForm.TimeZoneId),
            CancellationToken.None);

        CloseAllModals();
        planForm = CreateBudgetPlanFormModel.CreateDefault();
        Snackbar.Add($"Created plan '{plan.Name}'.", Severity.Success);
        await LoadPlansAsync(plan.PlanId, resetForecastWindow: true);
    }

    private async Task DeletePlanAsync()
    {
        if (!deletePlanId.HasValue)
        {
            return;
        }

        var deletedPlanId = deletePlanId.Value;
        var deletedPlanName = deletePlanName;

        await BudgetPlanService.DeleteAsync(deletedPlanId, CancellationToken.None);

        CloseAllModals();
        Snackbar.Add($"Deleted plan '{deletedPlanName}'.", Severity.Success);

        bool deletedSelectedPlan = selectedPlanId == deletedPlanId;
        await LoadPlansAsync(
            preferredPlanId: deletedSelectedPlan ? null : selectedPlanId,
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
        isLoading = true;

        try
        {
            var previousSelection = selectedPlanId;

            plans.Clear();
            plans.AddRange(await BudgetPlanService.ListAsync(CancellationToken.None));

            if (plans.Count == 0)
            {
                selectedPlan = null;
                selectedPlanId = null;
                forecastResult = null;
                transactionForecastResult = null;
                CloseAllModals();
                return;
            }

            var targetPlanId = preferredPlanId ?? previousSelection;
            var selectedPlanCandidate = targetPlanId.HasValue
                ? plans.FirstOrDefault(plan => plan.PlanId == targetPlanId.Value)
                : null;

            var resolvedPlan = selectedPlanCandidate ?? plans[0];
            bool selectionChanged = selectedPlanId != resolvedPlan.PlanId;

            selectedPlan = resolvedPlan;
            selectedPlanId = resolvedPlan.PlanId;
            balanceForm = BalanceUpdateFormModel.CreateDefault(resolvedPlan.StartingBalance.Amount, resolvedPlan.BalanceAsOfDate);

            if (resetForecastWindow || selectionChanged || forecastForm.StartDate is null || forecastForm.EndDate is null)
            {
                ResetForecastWindow();
            }

            await RunForecastAsync(showSnackbar: false);
        }
        finally
        {
            isLoading = false;
        }
    }

    private void OpenCreatePlanModal()
    {
        CloseAllModals();
        isEditingPlan = false;
        planForm = CreateBudgetPlanFormModel.CreateDefault();
        showPlanModal = true;
    }

    private void OpenDeletePlanModal()
    {
        if (selectedPlan is null)
        {
            return;
        }

        CloseAllModals();
        deletePlanId = selectedPlan.PlanId;
        deletePlanName = selectedPlan.Name;
        showDeletePlanModal = true;
    }

    private void OpenEditPlanModal()
    {
        if (selectedPlan is null)
        {
            return;
        }

        CloseAllModals();
        editingPlanId = selectedPlan.PlanId;
        isEditingPlan = true;
        planForm = CreateBudgetPlanFormModel.CreateFromPlan(selectedPlan);
        showPlanModal = true;
    }

    private void ResetForecastWindow()
        => forecastForm = ForecastFormModel.CreateDefault(durationDays: 365);

    private async Task RunForecastAsync()
        => await RunForecastAsync(showSnackbar: true);

    private async Task RunForecastAsync(bool showSnackbar)
    {
        if (selectedPlan is null)
        {
            forecastResult = null;
            transactionForecastResult = null;
            return;
        }

        var forecastStart = ToDateOnly(forecastForm.StartDate);
        var forecastEnd = ToDateOnly(forecastForm.EndDate);
        var transactionListRange = BuildTransactionListRange(forecastStart, forecastEnd, Today);

        var combinedResult = await BudgetPlanService.ForecastAsync(
            selectedPlan.PlanId,
            new ForecastRequest(transactionListRange.Start, transactionListRange.End),
            CancellationToken.None);

        transactionForecastResult = combinedResult;
        forecastResult = SliceForecastResult(combinedResult, forecastStart, forecastEnd);

        if (showSnackbar)
        {
            Snackbar.Add("Forecast calculated.", Severity.Success);
        }
    }

    private async Task SaveBalanceAsync()
    {
        if (selectedPlan is null)
        {
            return;
        }

        var updatedPlan = await BudgetPlanService.UpdateStartingBalanceAsync(
            selectedPlan.PlanId,
            new UpdateStartingBalanceRequest(
                balanceForm.Amount ?? 0m,
                ToDateOnly(balanceForm.BalanceAsOfDate)),
            CancellationToken.None);

        Snackbar.Add("Balance checkpoint updated.", Severity.Success);
        await LoadPlansAsync(updatedPlan.PlanId, resetForecastWindow: false);
    }

    private async Task SavePlanAsync()
    {
        if (isEditingPlan)
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
        if (!editingPlanId.HasValue)
        {
            return;
        }

        var updatedPlan = await BudgetPlanService.UpdateAsync(
            editingPlanId.Value,
            new UpdateBudgetPlanRequest(
                planForm.Name,
                planForm.StartingBalance ?? 0m,
                ToDateOnly(planForm.BalanceAsOfDate),
                planForm.TimeZoneId),
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

    private static ForecastResult SliceForecastResult(ForecastResult source, DateOnly start, DateOnly end)
    {
        if (source.Range.Start == start && source.Range.End == end)
        {
            return source;
        }

        var dailyPoints = source.DailyPoints
            .Where(point => point.Date >= start && point.Date <= end)
            .ToList();

        if (dailyPoints.Count == 0)
        {
            return source;
        }

        var occurrences = source.Occurrences
            .Where(occurrence => occurrence.Date >= start && occurrence.Date <= end)
            .ToList();

        var minPoint = dailyPoints.MinBy(point => point.EndOfDayBalance.Amount)!;
        var maxPoint = dailyPoints.MaxBy(point => point.EndOfDayBalance.Amount)!;
        var belowZeroDates = dailyPoints
            .Where(point => point.EndOfDayBalance.Amount < 0m)
            .Select(point => point.Date)
            .ToList();

        return new ForecastResult(
            new PredictiveBudget.Domain.Common.DateRange(start, end),
            dailyPoints,
            new ForecastSummary(
                minPoint.EndOfDayBalance,
                minPoint.Date,
                maxPoint.EndOfDayBalance,
                maxPoint.Date,
                belowZeroDates.Count == 0 ? null : belowZeroDates[0]),
            belowZeroDates,
            occurrences);
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

