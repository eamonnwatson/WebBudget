using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Web.Components.Pages.Models;

namespace PredictiveBudget.Web.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly Weekday[] WeekdayOptions =
    [
        Weekday.Monday,
        Weekday.Tuesday,
        Weekday.Wednesday,
        Weekday.Thursday,
        Weekday.Friday,
        Weekday.Saturday,
        Weekday.Sunday
    ];

    private static readonly (int Number, string Label)[] MonthOptions =
        Enumerable.Range(1, 12)
            .Select(month => (month, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month)))
            .ToArray();

    private readonly List<BudgetPlan> _plans = [];
    private CreateBudgetPlanFormModel _createForm = CreateBudgetPlanFormModel.CreateDefault();
    private BalanceUpdateFormModel _balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private RecurringRuleFormModel _recurringRuleForm = RecurringRuleFormModel.CreateDefault();
    private PlannedTransactionFormModel _plannedTransactionForm = PlannedTransactionFormModel.CreateDefault();
    private OccurrenceOverrideFormModel _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private ForecastFormModel _forecastForm = ForecastFormModel.CreateDefault();

    private BudgetPlan? _selectedPlan;
    private ForecastResult? _forecastResult;
    private Guid? _selectedPlanId;
    private Guid? _editingRecurringRuleId;
    private Guid? _editingPlannedTransactionId;
    private Guid? _editingOverrideId;
    private bool _isLoading = true;
    private bool _showBalanceModal;
    private bool _showRecurringRuleModal;
    private bool _showPlannedTransactionModal;
    private bool _showOverrideModal;

    protected override async Task OnInitializedAsync()
        => await LoadPlansAsync(resetForecastWindow: true);

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
        await LoadPlansAsync(plan.PlanId, resetForecastWindow: true);
    }

    private void OpenBalanceModal()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        CloseAllModals();
        _balanceForm = BalanceUpdateFormModel.CreateDefault(_selectedPlan.StartingBalance.Amount, _selectedPlan.BalanceAsOfDate);
        _showBalanceModal = true;
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

        CloseAllModals();
        Snackbar.Add("Balance checkpoint updated.", Severity.Success);
        await LoadPlansAsync(updatedPlan.PlanId, resetForecastWindow: true);
    }

    private void OpenAddRecurringRuleModal()
    {
        CloseAllModals();
        _editingRecurringRuleId = null;
        _recurringRuleForm = CreateRecurringRuleForm();
        _showRecurringRuleModal = true;
    }

    private void OpenEditRecurringRuleModal(Guid ruleId)
    {
        if (_selectedPlan is null)
        {
            return;
        }

        var rule = _selectedPlan.RecurringRules.FirstOrDefault(candidate => candidate.RuleId == ruleId);
        if (rule is null)
        {
            return;
        }

        CloseAllModals();
        _editingRecurringRuleId = rule.RuleId;
        _recurringRuleForm = CreateRecurringRuleForm(rule);
        _showRecurringRuleModal = true;
    }

    private async Task SaveRecurringRuleAsync()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        if (_editingRecurringRuleId.HasValue)
        {
            await BudgetPlanService.UpdateRecurringRuleAsync(
                _selectedPlan.PlanId,
                _editingRecurringRuleId.Value,
                new UpdateRecurringRuleRequest(
                    _recurringRuleForm.Name,
                    _recurringRuleForm.Direction,
                    _recurringRuleForm.Amount ?? 0m,
                    ToDateOnly(_recurringRuleForm.EffectiveStartDate),
                    _recurringRuleForm.EffectiveEndDate is null ? null : ToDateOnly(_recurringRuleForm.EffectiveEndDate),
                    _recurringRuleForm.Pattern,
                    _recurringRuleForm.IntervalWeeks ?? 1,
                    _recurringRuleForm.SelectedWeekdays.ToArray(),
                    _recurringRuleForm.IntervalMonths ?? 1,
                    _recurringRuleForm.SelectedMonths.ToArray(),
                    _recurringRuleForm.DayOfMonth ?? 1,
                    _recurringRuleForm.BusinessDayAdjustment,
                    _recurringRuleForm.IsActive,
                    _recurringRuleForm.DefaultAlertDaysBefore),
                CancellationToken.None);

            Snackbar.Add("Recurring rule updated.", Severity.Success);
        }
        else
        {
            await BudgetPlanService.AddRecurringRuleAsync(
                _selectedPlan.PlanId,
                new AddRecurringRuleRequest(
                    _recurringRuleForm.Name,
                    _recurringRuleForm.Direction,
                    _recurringRuleForm.Amount ?? 0m,
                    ToDateOnly(_recurringRuleForm.EffectiveStartDate),
                    _recurringRuleForm.EffectiveEndDate is null ? null : ToDateOnly(_recurringRuleForm.EffectiveEndDate),
                    _recurringRuleForm.Pattern,
                    _recurringRuleForm.IntervalWeeks ?? 1,
                    _recurringRuleForm.SelectedWeekdays.ToArray(),
                    _recurringRuleForm.IntervalMonths ?? 1,
                    _recurringRuleForm.SelectedMonths.ToArray(),
                    _recurringRuleForm.DayOfMonth ?? 1,
                    _recurringRuleForm.BusinessDayAdjustment,
                    _recurringRuleForm.IsActive,
                    _recurringRuleForm.DefaultAlertDaysBefore),
                CancellationToken.None);

            Snackbar.Add("Recurring rule added.", Severity.Success);
        }

        CloseAllModals();
        await LoadPlansAsync(_selectedPlan.PlanId, resetForecastWindow: false);
    }

    private void OpenAddPlannedTransactionModal()
    {
        CloseAllModals();
        _editingPlannedTransactionId = null;
        _plannedTransactionForm = CreatePlannedTransactionForm();
        _showPlannedTransactionModal = true;
    }

    private void OpenEditPlannedTransactionModal(Guid transactionId)
    {
        if (_selectedPlan is null)
        {
            return;
        }

        var transaction = _selectedPlan.PlannedTransactions.FirstOrDefault(candidate => candidate.TransactionId == transactionId);
        if (transaction is null)
        {
            return;
        }

        CloseAllModals();
        _editingPlannedTransactionId = transaction.TransactionId;
        _plannedTransactionForm = new PlannedTransactionFormModel
        {
            Date = transaction.Date.ToDateTime(TimeOnly.MinValue),
            Name = transaction.Name,
            Direction = transaction.Direction,
            Amount = transaction.Amount.Amount
        };
        _showPlannedTransactionModal = true;
    }

    private async Task SavePlannedTransactionAsync()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        if (_editingPlannedTransactionId.HasValue)
        {
            await BudgetPlanService.UpdatePlannedTransactionAsync(
                _selectedPlan.PlanId,
                _editingPlannedTransactionId.Value,
                new UpdatePlannedTransactionRequest(
                    ToDateOnly(_plannedTransactionForm.Date),
                    _plannedTransactionForm.Name,
                    _plannedTransactionForm.Direction,
                    _plannedTransactionForm.Amount ?? 0m),
                CancellationToken.None);

            Snackbar.Add("Planned transaction updated.", Severity.Success);
        }
        else
        {
            await BudgetPlanService.AddPlannedTransactionAsync(
                _selectedPlan.PlanId,
                new AddPlannedTransactionRequest(
                    ToDateOnly(_plannedTransactionForm.Date),
                    _plannedTransactionForm.Name,
                    _plannedTransactionForm.Direction,
                    _plannedTransactionForm.Amount ?? 0m),
                CancellationToken.None);

            Snackbar.Add("Planned transaction added.", Severity.Success);
        }

        CloseAllModals();
        await LoadPlansAsync(_selectedPlan.PlanId, resetForecastWindow: false);
    }

    private void OpenAddOverrideModal()
    {
        if (!CanEditOverrides)
        {
            return;
        }

        CloseAllModals();
        _editingOverrideId = null;
        _overrideForm = CreateOverrideForm();
        SyncOverrideSourceSelection();
        _showOverrideModal = true;
    }

    private void OpenEditOverrideModal(Guid overrideId)
    {
        if (_selectedPlan is null)
        {
            return;
        }

        var overrideEntry = _selectedPlan.Overrides.FirstOrDefault(candidate => candidate.OverrideId == overrideId);
        if (overrideEntry is null)
        {
            return;
        }

        CloseAllModals();
        _editingOverrideId = overrideEntry.OverrideId;
        _overrideForm = new OccurrenceOverrideFormModel
        {
            Source = overrideEntry.Source,
            SourceId = overrideEntry.SourceId.ToString(),
            OriginalDate = overrideEntry.OriginalDate.ToDateTime(TimeOnly.MinValue),
            Action = overrideEntry.Action,
            NewDate = overrideEntry.NewDate?.ToDateTime(TimeOnly.MinValue),
            NewAmount = overrideEntry.NewAmount?.Amount,
            NewName = overrideEntry.NewName
        };
        SyncOverrideSourceSelection();
        _showOverrideModal = true;
    }

    private async Task SaveOverrideAsync()
    {
        if (_selectedPlan is null)
        {
            return;
        }

        if (_editingOverrideId.HasValue)
        {
            await BudgetPlanService.UpdateOverrideAsync(
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
            await BudgetPlanService.AddOverrideAsync(
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

        CloseAllModals();
        await LoadPlansAsync(_selectedPlan.PlanId, resetForecastWindow: false);
    }

    private async Task RunForecastAsync()
        => await RunForecastAsync(showSnackbar: true);

    private async Task RunForecastAsync(bool showSnackbar)
    {
        if (_selectedPlan is null)
        {
            _forecastResult = null;
            return;
        }

        _forecastResult = await BudgetPlanService.ForecastAsync(
            _selectedPlan.PlanId,
            new ForecastRequest(
                ToDateOnly(_forecastForm.StartDate),
                ToDateOnly(_forecastForm.EndDate)),
            CancellationToken.None);

        if (showSnackbar)
        {
            Snackbar.Add("Forecast calculated.", Severity.Success);
        }
    }

    private void OnOverrideSourceChanged(OccurrenceSource source)
    {
        _overrideForm.Source = source;
        SyncOverrideSourceSelection();
    }

    private bool IsWeekdaySelected(Weekday weekday)
        => _recurringRuleForm.SelectedWeekdays.Contains(weekday);

    private void SetWeekday(Weekday weekday, bool isSelected)
    {
        if (isSelected)
        {
            _recurringRuleForm.SelectedWeekdays.Add(weekday);
        }
        else
        {
            _recurringRuleForm.SelectedWeekdays.Remove(weekday);
        }
    }

    private bool IsMonthSelected(int month)
        => _recurringRuleForm.SelectedMonths.Contains(month);

    private void SetMonth(int month, bool isSelected)
    {
        if (isSelected)
        {
            _recurringRuleForm.SelectedMonths.Add(month);
        }
        else
        {
            _recurringRuleForm.SelectedMonths.Remove(month);
        }
    }

    private IReadOnlyList<SourceOption> GetSourceOptions(OccurrenceSource source)
    {
        if (_selectedPlan is null)
        {
            return [];
        }

        return source switch
        {
            OccurrenceSource.RecurringRule => _selectedPlan.RecurringRules
                .OrderBy(rule => rule.Name)
                .Select(rule => new SourceOption(rule.RuleId.ToString(), rule.Name))
                .ToList(),
            OccurrenceSource.PlannedTransaction => _selectedPlan.PlannedTransactions
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Name)
                .Select(transaction => new SourceOption(transaction.TransactionId.ToString(), transaction.Name))
                .ToList(),
            _ => []
        };
    }

    private string GetOverrideSourceLabel(OccurrenceOverride overrideEntry)
    {
        if (_selectedPlan is null)
        {
            return overrideEntry.Source.ToString();
        }

        return overrideEntry.Source switch
        {
            OccurrenceSource.RecurringRule => _selectedPlan.RecurringRules.FirstOrDefault(rule => rule.RuleId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
            OccurrenceSource.PlannedTransaction => _selectedPlan.PlannedTransactions.FirstOrDefault(transaction => transaction.TransactionId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
            _ => overrideEntry.SourceId.ToString()
        };
    }

    private static string DescribeEffectiveWindow(RecurringTransactionRule rule)
        => rule.EffectiveEndDate is null
            ? $"{FormatDate(rule.EffectiveStartDate)} onward"
            : $"{FormatDate(rule.EffectiveStartDate)} to {FormatDate(rule.EffectiveEndDate.Value)}";

    private static string DescribeRecurrence(RecurrenceRule recurrence)
        => recurrence switch
        {
            WeeklyRecurrence weekly => $"Every {weekly.IntervalWeeks} week(s) on {string.Join(", ", weekly.Weekdays.OrderBy(day => day))}",
            MonthlyByDayOfMonthRecurrence monthly => $"Every {monthly.IntervalMonths} month(s) on day {monthly.DayOfMonth}",
            YearlyByMonthsAndDayRecurrence yearly => $"Yearly on {string.Join(", ", yearly.Months.OrderBy(month => month).Select(month => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month)))} day {yearly.DayOfMonth}",
            _ => recurrence.GetType().Name
        };

    private static string DescribeOverride(OccurrenceOverride overrideEntry)
        => overrideEntry.Action switch
        {
            OverrideAction.Skip => "Skip this occurrence",
            OverrideAction.MoveToDate when overrideEntry.NewDate.HasValue => $"Move to {FormatDate(overrideEntry.NewDate.Value)}",
            OverrideAction.ReplaceAmount when overrideEntry.NewAmount.HasValue => $"Use {FormatMoney(overrideEntry.NewAmount.Value)}",
            OverrideAction.ReplaceName when !string.IsNullOrWhiteSpace(overrideEntry.NewName) => $"Rename to {overrideEntry.NewName}",
            _ => overrideEntry.Action.ToString()
        };

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

    private bool CanEditOverrides
        => _selectedPlan is not null
           && (_selectedPlan.RecurringRules.Count > 0 || _selectedPlan.PlannedTransactions.Count > 0);

    private string RecurringRuleModalTitle
        => _editingRecurringRuleId.HasValue ? "Edit recurring rule" : "Add recurring rule";

    private string PlannedTransactionModalTitle
        => _editingPlannedTransactionId.HasValue ? "Edit planned transaction" : "Add planned transaction";

    private string OverrideModalTitle
        => _editingOverrideId.HasValue ? "Edit occurrence override" : "Add occurrence override";

    private void CloseAllModals()
    {
        _showBalanceModal = false;
        _showRecurringRuleModal = false;
        _showPlannedTransactionModal = false;
        _showOverrideModal = false;
        _editingRecurringRuleId = null;
        _editingPlannedTransactionId = null;
        _editingOverrideId = null;
    }

    private void ResetForecastWindow(BudgetPlan plan)
        => _forecastForm = ForecastFormModel.CreateDefault(plan.BalanceAsOfDate, durationDays: 365);

    private RecurringRuleFormModel CreateRecurringRuleForm(RecurringTransactionRule? rule = null)
    {
        if (rule is null)
        {
            var form = RecurringRuleFormModel.CreateDefault();
            if (_selectedPlan is not null)
            {
                form.EffectiveStartDate = _selectedPlan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
            }

            return form;
        }

        var recurringRuleForm = new RecurringRuleFormModel
        {
            Name = rule.Name,
            Direction = rule.Direction,
            Amount = rule.Amount.Amount,
            EffectiveStartDate = rule.EffectiveStartDate.ToDateTime(TimeOnly.MinValue),
            EffectiveEndDate = rule.EffectiveEndDate?.ToDateTime(TimeOnly.MinValue),
            BusinessDayAdjustment = rule.Recurrence.BusinessDayAdjustment,
            IsActive = rule.IsActive,
            DefaultAlertDaysBefore = rule.DefaultAlertDaysBefore
        };

        recurringRuleForm.SelectedWeekdays.Clear();
        recurringRuleForm.SelectedMonths.Clear();

        switch (rule.Recurrence)
        {
            case WeeklyRecurrence weekly:
                recurringRuleForm.Pattern = RecurrencePattern.Weekly;
                recurringRuleForm.IntervalWeeks = weekly.IntervalWeeks;
                foreach (var weekday in weekly.Weekdays)
                {
                    recurringRuleForm.SelectedWeekdays.Add(weekday);
                }
                break;
            case MonthlyByDayOfMonthRecurrence monthly:
                recurringRuleForm.Pattern = RecurrencePattern.MonthlyByDayOfMonth;
                recurringRuleForm.IntervalMonths = monthly.IntervalMonths;
                recurringRuleForm.DayOfMonth = monthly.DayOfMonth;
                break;
            case YearlyByMonthsAndDayRecurrence yearly:
                recurringRuleForm.Pattern = RecurrencePattern.YearlyByMonthsAndDay;
                recurringRuleForm.DayOfMonth = yearly.DayOfMonth;
                foreach (var month in yearly.Months)
                {
                    recurringRuleForm.SelectedMonths.Add(month);
                }
                break;
        }

        return recurringRuleForm;
    }

    private PlannedTransactionFormModel CreatePlannedTransactionForm()
    {
        var form = PlannedTransactionFormModel.CreateDefault();
        if (_selectedPlan is not null)
        {
            form.Date = _selectedPlan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
        }

        return form;
    }

    private OccurrenceOverrideFormModel CreateOverrideForm()
    {
        var form = OccurrenceOverrideFormModel.CreateDefault();
        if (_selectedPlan is not null)
        {
            form.OriginalDate = _selectedPlan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
        }

        return form;
    }

    private void SyncOverrideSourceSelection()
    {
        var options = GetSourceOptions(_overrideForm.Source);
        if (options.Count == 0)
        {
            _overrideForm.SourceId = string.Empty;
            return;
        }

        if (options.All(option => option.Id != _overrideForm.SourceId))
        {
            _overrideForm.SourceId = options[0].Id;
        }
    }
}
