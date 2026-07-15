using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Web.Features.BudgetPlans.Models;
using PredictiveBudget.Web.Services;

namespace PredictiveBudget.Web.Features.BudgetPlans.Workspace;

/// <summary>
/// Drives the detailed plan workspace, including rule, transaction, and override management.
/// </summary>
public partial class PlanDetails : ComponentBase
{
    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public Guid PlanId { get; set; }

    private enum DeleteTargetKind
    {
        RecurringRule,
        PlannedTransaction,
        Override
    }

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

    private BudgetPlan? _plan;
    private BalanceUpdateFormModel _balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private CreateBudgetPlanFormModel _planForm = CreateBudgetPlanFormModel.CreateDefault();
    private RecurringRuleFormModel _recurringRuleForm = RecurringRuleFormModel.CreateDefault();
    private PlannedTransactionFormModel _plannedTransactionForm = PlannedTransactionFormModel.CreateDefault();
    private OccurrenceOverrideFormModel _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private Guid? _editingRecurringRuleId;
    private Guid? _editingPlannedTransactionId;
    private Guid? _editingOverrideId;
    private HashSet<DateOnly> _overrideValidDates = [];
    private Guid? _deleteTargetId;
    private DeleteTargetKind? _deleteTargetKind;
    private string _deleteModalTitle = string.Empty;
    private string _deleteModalMessage = string.Empty;
    private bool _showRecurringRuleModal;
    private bool _showPlannedTransactionModal;
    private bool _showOverrideModal;
    private bool _showDeleteModal;
    private bool _showPlanSettingsModal;
    private bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
        => await LoadPlanAsync();

    private async Task LoadPlanAsync()
    {
        _isLoading = true;

        try
        {
            _plan = await BudgetPlanService.GetAsync(PlanId, CancellationToken.None);

            if (_plan is not null)
            {
                _plan = await BudgetPlanService.EnsureCalendarSubscriptionTokenAsync(PlanId, CancellationToken.None);
                // Keep the quick balance editor aligned with the latest persisted checkpoint.
                _balanceForm = BalanceUpdateFormModel.CreateDefault(_plan.StartingBalance.Amount, _plan.BalanceAsOfDate);
                _planForm = CreateBudgetPlanFormModel.CreateFromPlan(_plan);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task UpdateBalanceAsync()
    {
        var updatedPlan = await BudgetPlanService.UpdateStartingBalanceAsync(
            PlanId,
            new UpdateStartingBalanceRequest(
                _balanceForm.Amount ?? 0m,
                ToDateOnly(_balanceForm.BalanceAsOfDate)),
            CancellationToken.None);

        ApplyUpdatedPlan(updatedPlan);
        Snackbar.Add("Starting balance updated.", Severity.Success);
    }

    private void OpenEditPlanModal()
    {
        if (_plan is null)
        {
            return;
        }

        CloseAllModals();
        _planForm = CreateBudgetPlanFormModel.CreateFromPlan(_plan);
        _showPlanSettingsModal = true;
    }

    private async Task SavePlanSettingsAsync()
    {
        var updatedPlan = await BudgetPlanService.UpdateAsync(
            PlanId,
            new UpdateBudgetPlanRequest(
                _planForm.Name,
                _planForm.StartingBalance ?? 0m,
                ToDateOnly(_planForm.BalanceAsOfDate),
                _planForm.TimeZoneId),
            CancellationToken.None);

        ApplyUpdatedPlan(updatedPlan);
        CloseAllModals();
        Snackbar.Add("Plan details updated.", Severity.Success);
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
        if (_plan is null)
        {
            return;
        }

        var rule = _plan.RecurringRules.FirstOrDefault(candidate => candidate.RuleId == ruleId);
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
        if (_plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // The same modal supports both create and edit flows, so branch on the tracked edit id.
        if (_editingRecurringRuleId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateRecurringRuleAsync(
                PlanId,
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
            updatedPlan = await BudgetPlanService.AddRecurringRuleAsync(
                PlanId,
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

        ApplyUpdatedPlan(updatedPlan);
        _recurringRuleForm = CreateRecurringRuleForm();
        CloseAllModals();
    }

    private Task AddRecurringRuleAsync()
    {
        _editingRecurringRuleId = null;
        return SaveRecurringRuleAsync();
    }

    private void OpenDeleteRecurringRuleConfirmation(Guid ruleId, string name)
        => OpenDeleteConfirmation(
            DeleteTargetKind.RecurringRule,
            ruleId,
            "Delete recurring rule",
            $"Delete '{name}'? Any overrides tied to this rule will also be removed.");

    private void OpenAddPlannedTransactionModal()
    {
        CloseAllModals();
        _editingPlannedTransactionId = null;
        _plannedTransactionForm = CreatePlannedTransactionForm();
        _showPlannedTransactionModal = true;
    }

    private void OpenEditPlannedTransactionModal(Guid transactionId)
    {
        if (_plan is null)
        {
            return;
        }

        var transaction = _plan.PlannedTransactions.FirstOrDefault(candidate => candidate.TransactionId == transactionId);
        if (transaction is null)
        {
            return;
        }

        CloseAllModals();
        _editingPlannedTransactionId = transaction.TransactionId;
        _plannedTransactionForm = CreatePlannedTransactionForm(transaction);
        _showPlannedTransactionModal = true;
    }

    private async Task SavePlannedTransactionAsync()
    {
        if (_plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // Reuse the same form model for add and edit to keep the modal workflow consistent.
        if (_editingPlannedTransactionId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdatePlannedTransactionAsync(
                PlanId,
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
            updatedPlan = await BudgetPlanService.AddPlannedTransactionAsync(
                PlanId,
                new AddPlannedTransactionRequest(
                    ToDateOnly(_plannedTransactionForm.Date),
                    _plannedTransactionForm.Name,
                    _plannedTransactionForm.Direction,
                    _plannedTransactionForm.Amount ?? 0m),
                CancellationToken.None);

            Snackbar.Add("Planned transaction added.", Severity.Success);
        }

        ApplyUpdatedPlan(updatedPlan);
        _plannedTransactionForm = CreatePlannedTransactionForm();
        CloseAllModals();
    }

    private Task AddPlannedTransactionAsync()
    {
        _editingPlannedTransactionId = null;
        return SavePlannedTransactionAsync();
    }

    private void OpenDeletePlannedTransactionConfirmation(Guid transactionId, string name)
        => OpenDeleteConfirmation(
            DeleteTargetKind.PlannedTransaction,
            transactionId,
            "Delete planned transaction",
            $"Delete '{name}'? Any overrides tied to this transaction will also be removed.");

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
        if (_plan is null)
        {
            return;
        }

        var overrideEntry = _plan.Overrides.FirstOrDefault(candidate => candidate.OverrideId == overrideId);
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
        SyncOverrideSourceSelection(defaultDate: false);
        _showOverrideModal = true;
    }

    private async Task SaveOverrideAsync()
    {
        if (_plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // Overrides follow the same shared create/edit pattern as the other workspace modals.
        if (_editingOverrideId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateOverrideAsync(
                PlanId,
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
                PlanId,
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

        ApplyUpdatedPlan(updatedPlan);
        _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
        CloseAllModals();
    }

    private Task AddOverrideAsync()
    {
        _editingOverrideId = null;
        return SaveOverrideAsync();
    }

    private void OpenDeleteOverrideConfirmation(Guid overrideId, string sourceLabel)
        => OpenDeleteConfirmation(
            DeleteTargetKind.Override,
            overrideId,
            "Delete occurrence override",
            $"Delete the override for '{sourceLabel}'?");

    private async Task ConfirmDeleteAsync()
    {
        if (_deleteTargetId is null || _deleteTargetKind is null)
        {
            return;
        }

        BudgetPlan updatedPlan = _deleteTargetKind.Value switch
        {
            DeleteTargetKind.RecurringRule => await BudgetPlanService.DeleteRecurringRuleAsync(PlanId, _deleteTargetId.Value, CancellationToken.None),
            DeleteTargetKind.PlannedTransaction => await BudgetPlanService.DeletePlannedTransactionAsync(PlanId, _deleteTargetId.Value, CancellationToken.None),
            DeleteTargetKind.Override => await BudgetPlanService.DeleteOverrideAsync(PlanId, _deleteTargetId.Value, CancellationToken.None),
            _ => throw new InvalidOperationException("Unknown delete target.")
        };

        ApplyUpdatedPlan(updatedPlan);
        CloseAllModals();
        Snackbar.Add("Item deleted.", Severity.Success);
    }

    private void OnOverrideSourceChanged(OccurrenceSource source)
    {
        _overrideForm.Source = source;
        SyncOverrideSourceSelection();
    }

    private static DateOnly ToDateOnly(DateTime? value)
        => DateOnly.FromDateTime(value ?? DateTime.Today);

    private static string FormatMoney(Money money)
        => $"{money.Amount:N2} {money.Currency}";

    private static string FormatSignedMoney(TransactionDirection direction, Money money)
    {
        string sign = direction == TransactionDirection.Outflow ? "-" : "+";
        return $"{sign}{money.Amount:N2} {money.Currency}";
    }

    private static Color GetDirectionColor(TransactionDirection direction)
        => direction == TransactionDirection.Inflow ? Color.Success : Color.Error;

    private static string FormatDate(DateOnly date)
        => date.ToString("MMM d, yyyy");

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
        if (_plan is null)
        {
            return [];
        }

        return source switch
        {
            OccurrenceSource.RecurringRule => _plan.RecurringRules
                .OrderBy(rule => rule.Name)
                .Select(rule => new SourceOption(rule.RuleId.ToString(), rule.Name))
                .ToList(),
            OccurrenceSource.PlannedTransaction => _plan.PlannedTransactions
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Name)
                .Select(transaction => new SourceOption(transaction.TransactionId.ToString(), transaction.Name))
                .ToList(),
            _ => []
        };
    }

    private string GetOverrideSourceLabel(OccurrenceOverride overrideEntry)
    {
        if (_plan is null)
        {
            return overrideEntry.Source.ToString();
        }

        return overrideEntry.Source switch
        {
            OccurrenceSource.RecurringRule => _plan.RecurringRules.FirstOrDefault(rule => rule.RuleId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
            OccurrenceSource.PlannedTransaction => _plan.PlannedTransactions.FirstOrDefault(transaction => transaction.TransactionId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
            _ => overrideEntry.SourceId.ToString()
        };
    }

    private static string DescribeOverride(OccurrenceOverride overrideEntry)
        => overrideEntry.Action switch
        {
            OverrideAction.Skip => "Skip this occurrence",
            OverrideAction.MoveToDate when overrideEntry.NewDate.HasValue => $"Move to {FormatDate(overrideEntry.NewDate.Value)}",
            OverrideAction.ReplaceAmount when overrideEntry.NewAmount.HasValue => $"Use {FormatMoney(overrideEntry.NewAmount.Value)}",
            OverrideAction.ReplaceName when !string.IsNullOrWhiteSpace(overrideEntry.NewName) => $"Rename to {overrideEntry.NewName}",
            _ => overrideEntry.Action.ToString()
        };

    private bool CanEditOverrides
        => _plan is not null
           && (_plan.RecurringRules.Count > 0 || _plan.PlannedTransactions.Count > 0);

    private string RecurringRuleModalTitle
        => _editingRecurringRuleId.HasValue ? "Edit recurring rule" : "Add recurring rule";

    private string PlannedTransactionModalTitle
        => _editingPlannedTransactionId.HasValue ? "Edit planned transaction" : "Add planned transaction";

    private string OverrideModalTitle
        => _editingOverrideId.HasValue ? "Edit occurrence override" : "Add occurrence override";

    private string? GetCalendarSubscriptionPath()
        => _plan is not null && !string.IsNullOrWhiteSpace(_plan.CalendarSubscriptionToken)
            ? CalendarSubscriptionService.BuildCalendarPath(_plan.PlanId, _plan.CalendarSubscriptionToken)
            : null;

    private string? GetCalendarSubscriptionUrl()
    {
        var relativePath = GetCalendarSubscriptionPath();
        if (relativePath is null)
        {
            return null;
        }

        return new Uri(new Uri(NavigationManager.BaseUri), relativePath.TrimStart('/')).ToString();
    }

    private void OpenDeleteConfirmation(DeleteTargetKind kind, Guid id, string title, string message)
    {
        CloseAllModals();
        _deleteTargetKind = kind;
        _deleteTargetId = id;
        _deleteModalTitle = title;
        _deleteModalMessage = message;
        _showDeleteModal = true;
    }

    private void ApplyUpdatedPlan(BudgetPlan updatedPlan)
    {
        _plan = updatedPlan;
        _balanceForm = BalanceUpdateFormModel.CreateDefault(updatedPlan.StartingBalance.Amount, updatedPlan.BalanceAsOfDate);
        _planForm = CreateBudgetPlanFormModel.CreateFromPlan(updatedPlan);
    }

    private void CloseAllModals()
    {
        _showPlanSettingsModal = false;
        _showRecurringRuleModal = false;
        _showPlannedTransactionModal = false;
        _showOverrideModal = false;
        _showDeleteModal = false;
        _editingRecurringRuleId = null;
        _editingPlannedTransactionId = null;
        _editingOverrideId = null;
        _deleteTargetId = null;
        _deleteTargetKind = null;
        _deleteModalTitle = string.Empty;
        _deleteModalMessage = string.Empty;
    }

    private RecurringRuleFormModel CreateRecurringRuleForm(RecurringTransactionRule? rule = null)
    {
        if (rule is null)
        {
            var form = RecurringRuleFormModel.CreateDefault();
            if (_plan is not null)
            {
                form.EffectiveStartDate = _plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
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

    private PlannedTransactionFormModel CreatePlannedTransactionForm(PlannedTransaction? transaction = null)
    {
        if (transaction is null)
        {
            var form = PlannedTransactionFormModel.CreateDefault();
            if (_plan is not null)
            {
                form.Date = _plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
            }

            return form;
        }

        return new PlannedTransactionFormModel
        {
            Date = transaction.Date.ToDateTime(TimeOnly.MinValue),
            Name = transaction.Name,
            Direction = transaction.Direction,
            Amount = transaction.Amount.Amount
        };
    }

    private OccurrenceOverrideFormModel CreateOverrideForm()
    {
        var form = OccurrenceOverrideFormModel.CreateDefault();
        if (_plan is not null)
        {
            form.OriginalDate = _plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
        }

        return form;
    }

    private void SyncOverrideSourceSelection(bool defaultDate = true)
    {
        var options = GetSourceOptions(_overrideForm.Source);
        if (options.Count == 0)
        {
            _overrideForm.SourceId = string.Empty;
            _overrideValidDates = [];
            return;
        }

        // Default to the first valid source whenever the source type changes or the previous choice disappears.
        if (options.All(option => option.Id != _overrideForm.SourceId))
        {
            _overrideForm.SourceId = options[0].Id;
            defaultDate = true;
        }

        _overrideValidDates = ComputeValidOccurrenceDates(_overrideForm.SourceId, _overrideForm.Source);

        if (defaultDate && _overrideValidDates.Count > 0)
        {
            _overrideForm.OriginalDate = GetNextOccurrenceDate(_overrideValidDates).ToDateTime(TimeOnly.MinValue);
        }
    }

    private void OnOverrideSourceItemChanged(string sourceId)
    {
        _overrideForm.SourceId = sourceId;
        _overrideValidDates = ComputeValidOccurrenceDates(sourceId, _overrideForm.Source);

        if (_overrideValidDates.Count > 0)
        {
            _overrideForm.OriginalDate = GetNextOccurrenceDate(_overrideValidDates).ToDateTime(TimeOnly.MinValue);
        }
    }

    private HashSet<DateOnly> ComputeValidOccurrenceDates(string sourceId, OccurrenceSource source)
    {
        if (_plan is null || string.IsNullOrEmpty(sourceId))
            return [];

        if (source == OccurrenceSource.RecurringRule)
        {
            if (!Guid.TryParse(sourceId, out var ruleId)) return [];
            var rule = _plan.RecurringRules.FirstOrDefault(r => r.RuleId == ruleId);
            if (rule is null) return [];

            var rangeEnd = rule.EffectiveEndDate ?? DateOnly.FromDateTime(DateTime.Today.AddYears(5));
            return rule.Recurrence
                .Expand(rule.EffectiveStartDate, rangeEnd, rule.EffectiveStartDate)
                .Where(d => rule.IsEffectiveOn(d))
                .ToHashSet();
        }

        if (source == OccurrenceSource.PlannedTransaction)
        {
            if (!Guid.TryParse(sourceId, out var txnId)) return [];
            var txn = _plan.PlannedTransactions.FirstOrDefault(t => t.TransactionId == txnId);
            if (txn is null) return [];

            return [txn.Date];
        }

        return [];
    }

    private static DateOnly GetNextOccurrenceDate(HashSet<DateOnly> dates)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return dates.Where(d => d >= today).OrderBy(d => d).FirstOrDefault()
               is DateOnly next && next != default
            ? next
            : dates.OrderBy(d => d).First();
    }

    private bool IsOriginalDateDisabled(DateTime dt)
    {
        if (_overrideValidDates.Count == 0) return false;
        return !_overrideValidDates.Contains(DateOnly.FromDateTime(dt));
    }
}
