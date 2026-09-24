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

    private BudgetPlan? plan;
    private BalanceUpdateFormModel balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private CreateBudgetPlanFormModel planForm = CreateBudgetPlanFormModel.CreateDefault();
    private RecurringRuleFormModel recurringRuleForm = RecurringRuleFormModel.CreateDefault();
    private PlannedTransactionFormModel plannedTransactionForm = PlannedTransactionFormModel.CreateDefault();
    private OccurrenceOverrideFormModel overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private Guid? editingRecurringRuleId;
    private Guid? editingPlannedTransactionId;
    private Guid? editingOverrideId;
    private HashSet<DateOnly> overrideValidDates = [];
    private Guid? deleteTargetId;
    private DeleteTargetKind? deleteTargetKind;
    private string deleteModalTitle = string.Empty;
    private string deleteModalMessage = string.Empty;
    private bool showRecurringRuleModal;
    private bool showPlannedTransactionModal;
    private bool showOverrideModal;
    private bool showDeleteModal;
    private bool showPlanSettingsModal;
    private bool isLoading = true;

    protected override async Task OnParametersSetAsync()
        => await LoadPlanAsync();

    private async Task LoadPlanAsync()
    {
        isLoading = true;

        try
        {
            plan = await BudgetPlanService.GetAsync(PlanId, CancellationToken.None);

            if (plan is not null)
            {
                plan = await BudgetPlanService.EnsureCalendarSubscriptionTokenAsync(PlanId, CancellationToken.None);
                // Keep the quick balance editor aligned with the latest persisted checkpoint.
                balanceForm = BalanceUpdateFormModel.CreateDefault(plan.StartingBalance.Amount, plan.BalanceAsOfDate);
                planForm = CreateBudgetPlanFormModel.CreateFromPlan(plan);
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task UpdateBalanceAsync()
    {
        var updatedPlan = await BudgetPlanService.UpdateStartingBalanceAsync(
            PlanId,
            new UpdateStartingBalanceRequest(
                balanceForm.Amount ?? 0m,
                ToDateOnly(balanceForm.BalanceAsOfDate)),
            CancellationToken.None);

        ApplyUpdatedPlan(updatedPlan);
        Snackbar.Add("Starting balance updated.", Severity.Success);
    }

    private void OpenEditPlanModal()
    {
        if (plan is null)
        {
            return;
        }

        CloseAllModals();
        planForm = CreateBudgetPlanFormModel.CreateFromPlan(plan);
        showPlanSettingsModal = true;
    }

    private async Task SavePlanSettingsAsync()
    {
        var updatedPlan = await BudgetPlanService.UpdateAsync(
            PlanId,
            new UpdateBudgetPlanRequest(
                planForm.Name,
                planForm.StartingBalance ?? 0m,
                ToDateOnly(planForm.BalanceAsOfDate),
                planForm.TimeZoneId),
            CancellationToken.None);

        ApplyUpdatedPlan(updatedPlan);
        CloseAllModals();
        Snackbar.Add("Plan details updated.", Severity.Success);
    }

    private void OpenAddRecurringRuleModal()
    {
        CloseAllModals();
        editingRecurringRuleId = null;
        recurringRuleForm = CreateRecurringRuleForm();
        showRecurringRuleModal = true;
    }

    private void OpenEditRecurringRuleModal(Guid ruleId)
    {
        if (plan is null)
        {
            return;
        }

        var rule = plan.RecurringRules.FirstOrDefault(candidate => candidate.RuleId == ruleId);
        if (rule is null)
        {
            return;
        }

        CloseAllModals();
        editingRecurringRuleId = rule.RuleId;
        recurringRuleForm = CreateRecurringRuleForm(rule);
        showRecurringRuleModal = true;
    }

    private async Task SaveRecurringRuleAsync()
    {
        if (plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // The same modal supports both create and edit flows, so branch on the tracked edit id.
        if (editingRecurringRuleId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateRecurringRuleAsync(
                PlanId,
                editingRecurringRuleId.Value,
                new UpdateRecurringRuleRequest(
                    recurringRuleForm.Name,
                    recurringRuleForm.Direction,
                    recurringRuleForm.Amount ?? 0m,
                    ToDateOnly(recurringRuleForm.EffectiveStartDate),
                    recurringRuleForm.EffectiveEndDate is null ? null : ToDateOnly(recurringRuleForm.EffectiveEndDate),
                    recurringRuleForm.Pattern,
                    recurringRuleForm.IntervalWeeks ?? 1,
                    recurringRuleForm.SelectedWeekdays.ToArray(),
                    recurringRuleForm.IntervalMonths ?? 1,
                    recurringRuleForm.SelectedMonths.ToArray(),
                    recurringRuleForm.DayOfMonth ?? 1,
                    recurringRuleForm.BusinessDayAdjustment,
                    recurringRuleForm.IsActive,
                    recurringRuleForm.DefaultAlertDaysBefore),
                CancellationToken.None);

            Snackbar.Add("Recurring rule updated.", Severity.Success);
        }
        else
        {
            updatedPlan = await BudgetPlanService.AddRecurringRuleAsync(
                PlanId,
                new AddRecurringRuleRequest(
                    recurringRuleForm.Name,
                    recurringRuleForm.Direction,
                    recurringRuleForm.Amount ?? 0m,
                    ToDateOnly(recurringRuleForm.EffectiveStartDate),
                    recurringRuleForm.EffectiveEndDate is null ? null : ToDateOnly(recurringRuleForm.EffectiveEndDate),
                    recurringRuleForm.Pattern,
                    recurringRuleForm.IntervalWeeks ?? 1,
                    recurringRuleForm.SelectedWeekdays.ToArray(),
                    recurringRuleForm.IntervalMonths ?? 1,
                    recurringRuleForm.SelectedMonths.ToArray(),
                    recurringRuleForm.DayOfMonth ?? 1,
                    recurringRuleForm.BusinessDayAdjustment,
                    recurringRuleForm.IsActive,
                    recurringRuleForm.DefaultAlertDaysBefore),
                CancellationToken.None);

            Snackbar.Add("Recurring rule added.", Severity.Success);
        }

        ApplyUpdatedPlan(updatedPlan);
        recurringRuleForm = CreateRecurringRuleForm();
        CloseAllModals();
    }

    private Task AddRecurringRuleAsync()
    {
        editingRecurringRuleId = null;
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
        editingPlannedTransactionId = null;
        plannedTransactionForm = CreatePlannedTransactionForm();
        showPlannedTransactionModal = true;
    }

    private void OpenEditPlannedTransactionModal(Guid transactionId)
    {
        if (plan is null)
        {
            return;
        }

        var transaction = plan.PlannedTransactions.FirstOrDefault(candidate => candidate.TransactionId == transactionId);
        if (transaction is null)
        {
            return;
        }

        CloseAllModals();
        editingPlannedTransactionId = transaction.TransactionId;
        plannedTransactionForm = CreatePlannedTransactionForm(transaction);
        showPlannedTransactionModal = true;
    }

    private async Task SavePlannedTransactionAsync()
    {
        if (plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // Reuse the same form model for add and edit to keep the modal workflow consistent.
        if (editingPlannedTransactionId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdatePlannedTransactionAsync(
                PlanId,
                editingPlannedTransactionId.Value,
                new UpdatePlannedTransactionRequest(
                    ToDateOnly(plannedTransactionForm.Date),
                    plannedTransactionForm.Name,
                    plannedTransactionForm.Direction,
                    plannedTransactionForm.Amount ?? 0m),
                CancellationToken.None);

            Snackbar.Add("Planned transaction updated.", Severity.Success);
        }
        else
        {
            updatedPlan = await BudgetPlanService.AddPlannedTransactionAsync(
                PlanId,
                new AddPlannedTransactionRequest(
                    ToDateOnly(plannedTransactionForm.Date),
                    plannedTransactionForm.Name,
                    plannedTransactionForm.Direction,
                    plannedTransactionForm.Amount ?? 0m),
                CancellationToken.None);

            Snackbar.Add("Planned transaction added.", Severity.Success);
        }

        ApplyUpdatedPlan(updatedPlan);
        plannedTransactionForm = CreatePlannedTransactionForm();
        CloseAllModals();
    }

    private Task AddPlannedTransactionAsync()
    {
        editingPlannedTransactionId = null;
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
        editingOverrideId = null;
        overrideForm = CreateOverrideForm();
        SyncOverrideSourceSelection();
        showOverrideModal = true;
    }

    private void OpenEditOverrideModal(Guid overrideId)
    {
        if (plan is null)
        {
            return;
        }

        var overrideEntry = plan.Overrides.FirstOrDefault(candidate => candidate.OverrideId == overrideId);
        if (overrideEntry is null)
        {
            return;
        }

        CloseAllModals();
        editingOverrideId = overrideEntry.OverrideId;
        overrideForm = new OccurrenceOverrideFormModel
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
        showOverrideModal = true;
    }

    private async Task SaveOverrideAsync()
    {
        if (plan is null)
        {
            return;
        }

        BudgetPlan updatedPlan;

        // Overrides follow the same shared create/edit pattern as the other workspace modals.
        if (editingOverrideId.HasValue)
        {
            updatedPlan = await BudgetPlanService.UpdateOverrideAsync(
                PlanId,
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
                PlanId,
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

        ApplyUpdatedPlan(updatedPlan);
        overrideForm = OccurrenceOverrideFormModel.CreateDefault();
        CloseAllModals();
    }

    private Task AddOverrideAsync()
    {
        editingOverrideId = null;
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
        if (deleteTargetId is null || deleteTargetKind is null)
        {
            return;
        }

        BudgetPlan updatedPlan = deleteTargetKind.Value switch
        {
            DeleteTargetKind.RecurringRule => await BudgetPlanService.DeleteRecurringRuleAsync(PlanId, deleteTargetId.Value, CancellationToken.None),
            DeleteTargetKind.PlannedTransaction => await BudgetPlanService.DeletePlannedTransactionAsync(PlanId, deleteTargetId.Value, CancellationToken.None),
            DeleteTargetKind.Override => await BudgetPlanService.DeleteOverrideAsync(PlanId, deleteTargetId.Value, CancellationToken.None),
            _ => throw new InvalidOperationException("Unknown delete target.")
        };

        ApplyUpdatedPlan(updatedPlan);
        CloseAllModals();
        Snackbar.Add("Item deleted.", Severity.Success);
    }

    private void OnOverrideSourceChanged(OccurrenceSource source)
    {
        overrideForm.Source = source;
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
        => recurringRuleForm.SelectedWeekdays.Contains(weekday);

    private void SetWeekday(Weekday weekday, bool isSelected)
    {
        if (isSelected)
        {
            recurringRuleForm.SelectedWeekdays.Add(weekday);
        }
        else
        {
            recurringRuleForm.SelectedWeekdays.Remove(weekday);
        }
    }

    private bool IsMonthSelected(int month)
        => recurringRuleForm.SelectedMonths.Contains(month);

    private void SetMonth(int month, bool isSelected)
    {
        if (isSelected)
        {
            recurringRuleForm.SelectedMonths.Add(month);
        }
        else
        {
            recurringRuleForm.SelectedMonths.Remove(month);
        }
    }

    private IReadOnlyList<SourceOption> GetSourceOptions(OccurrenceSource source)
    {
        if (plan is null)
        {
            return [];
        }

        return source switch
        {
            OccurrenceSource.RecurringRule => plan.RecurringRules
                .OrderBy(rule => rule.Name)
                .Select(rule => new SourceOption(rule.RuleId.ToString(), rule.Name))
                .ToList(),
            OccurrenceSource.PlannedTransaction => plan.PlannedTransactions
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.Name)
                .Select(transaction => new SourceOption(transaction.TransactionId.ToString(), transaction.Name))
                .ToList(),
            _ => []
        };
    }

    private string GetOverrideSourceLabel(OccurrenceOverride overrideEntry)
    {
        if (plan is null)
        {
            return overrideEntry.Source.ToString();
        }

        return overrideEntry.Source switch
        {
            OccurrenceSource.RecurringRule => plan.RecurringRules.FirstOrDefault(rule => rule.RuleId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
            OccurrenceSource.PlannedTransaction => plan.PlannedTransactions.FirstOrDefault(transaction => transaction.TransactionId == overrideEntry.SourceId)?.Name ?? overrideEntry.SourceId.ToString(),
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
        => plan is not null
           && (plan.RecurringRules.Count > 0 || plan.PlannedTransactions.Count > 0);

    private string RecurringRuleModalTitle
        => editingRecurringRuleId.HasValue ? "Edit recurring rule" : "Add recurring rule";

    private string PlannedTransactionModalTitle
        => editingPlannedTransactionId.HasValue ? "Edit planned transaction" : "Add planned transaction";

    private string OverrideModalTitle
        => editingOverrideId.HasValue ? "Edit occurrence override" : "Add occurrence override";

    private string? GetCalendarSubscriptionPath()
        => plan is not null && !string.IsNullOrWhiteSpace(plan.CalendarSubscriptionToken)
            ? CalendarSubscriptionService.BuildCalendarPath(plan.PlanId, plan.CalendarSubscriptionToken)
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
        deleteTargetKind = kind;
        deleteTargetId = id;
        deleteModalTitle = title;
        deleteModalMessage = message;
        showDeleteModal = true;
    }

    private void ApplyUpdatedPlan(BudgetPlan updatedPlan)
    {
        plan = updatedPlan;
        balanceForm = BalanceUpdateFormModel.CreateDefault(updatedPlan.StartingBalance.Amount, updatedPlan.BalanceAsOfDate);
        planForm = CreateBudgetPlanFormModel.CreateFromPlan(updatedPlan);
    }

    private void CloseAllModals()
    {
        showPlanSettingsModal = false;
        showRecurringRuleModal = false;
        showPlannedTransactionModal = false;
        showOverrideModal = false;
        showDeleteModal = false;
        editingRecurringRuleId = null;
        editingPlannedTransactionId = null;
        editingOverrideId = null;
        deleteTargetId = null;
        deleteTargetKind = null;
        deleteModalTitle = string.Empty;
        deleteModalMessage = string.Empty;
    }

    private RecurringRuleFormModel CreateRecurringRuleForm(RecurringTransactionRule? rule = null)
    {
        if (rule is null)
        {
            var form = RecurringRuleFormModel.CreateDefault();
            if (plan is not null)
            {
                form.EffectiveStartDate = plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
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
            if (plan is not null)
            {
                form.Date = plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
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
        if (plan is not null)
        {
            form.OriginalDate = plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue);
        }

        return form;
    }

    private void SyncOverrideSourceSelection(bool defaultDate = true)
    {
        var options = GetSourceOptions(overrideForm.Source);
        if (options.Count == 0)
        {
            overrideForm.SourceId = string.Empty;
            overrideValidDates = [];
            return;
        }

        // Default to the first valid source whenever the source type changes or the previous choice disappears.
        if (options.All(option => option.Id != overrideForm.SourceId))
        {
            overrideForm.SourceId = options[0].Id;
            defaultDate = true;
        }

        overrideValidDates = ComputeValidOccurrenceDates(overrideForm.SourceId, overrideForm.Source);

        if (defaultDate && overrideValidDates.Count > 0)
        {
            overrideForm.OriginalDate = GetNextOccurrenceDate(overrideValidDates).ToDateTime(TimeOnly.MinValue);
        }
    }

    private void OnOverrideSourceItemChanged(string sourceId)
    {
        overrideForm.SourceId = sourceId;
        overrideValidDates = ComputeValidOccurrenceDates(sourceId, overrideForm.Source);

        if (overrideValidDates.Count > 0)
        {
            overrideForm.OriginalDate = GetNextOccurrenceDate(overrideValidDates).ToDateTime(TimeOnly.MinValue);
        }
    }

    private HashSet<DateOnly> ComputeValidOccurrenceDates(string sourceId, OccurrenceSource source)
    {
        if (plan is null || string.IsNullOrEmpty(sourceId))
            return [];

        if (source == OccurrenceSource.RecurringRule)
        {
            if (!Guid.TryParse(sourceId, out var ruleId)) return [];
            var rule = plan.RecurringRules.FirstOrDefault(r => r.RuleId == ruleId);
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
            var txn = plan.PlannedTransactions.FirstOrDefault(t => t.TransactionId == txnId);
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
        if (overrideValidDates.Count == 0) return false;
        return !overrideValidDates.Contains(DateOnly.FromDateTime(dt));
    }
}

