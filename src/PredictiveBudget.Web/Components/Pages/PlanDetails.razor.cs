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

public partial class PlanDetails : ComponentBase
{
    [Inject] private BudgetPlanService BudgetPlanService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public Guid PlanId { get; set; }

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
    private ForecastResult? _forecastResult;
    private BalanceUpdateFormModel _balanceForm = BalanceUpdateFormModel.CreateDefault(0m, DateOnly.FromDateTime(DateTime.Today));
    private RecurringRuleFormModel _recurringRuleForm = RecurringRuleFormModel.CreateDefault();
    private PlannedTransactionFormModel _plannedTransactionForm = PlannedTransactionFormModel.CreateDefault();
    private OccurrenceOverrideFormModel _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
    private ForecastFormModel _forecastForm = ForecastFormModel.CreateDefault();
    private bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
        => await LoadPlanAsync();

    private async Task LoadPlanAsync()
    {
        _isLoading = true;
        _plan = await BudgetPlanService.GetAsync(PlanId, CancellationToken.None);

        if (_plan is not null)
        {
            _balanceForm = BalanceUpdateFormModel.CreateDefault(_plan.StartingBalance.Amount, _plan.BalanceAsOfDate);
        }

        _isLoading = false;
    }

    private async Task UpdateBalanceAsync()
    {
        var updatedPlan = await BudgetPlanService.UpdateStartingBalanceAsync(
            PlanId,
            new UpdateStartingBalanceRequest(
                _balanceForm.Amount ?? 0m,
                ToDateOnly(_balanceForm.BalanceAsOfDate)),
            CancellationToken.None);

        _plan = updatedPlan;
        Snackbar.Add("Starting balance updated.", Severity.Success);
    }

    private async Task AddRecurringRuleAsync()
    {
        var updatedPlan = await BudgetPlanService.AddRecurringRuleAsync(
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

        _plan = updatedPlan;
        _recurringRuleForm = RecurringRuleFormModel.CreateDefault();
        Snackbar.Add("Recurring rule added.", Severity.Success);
    }

    private async Task AddPlannedTransactionAsync()
    {
        var updatedPlan = await BudgetPlanService.AddPlannedTransactionAsync(
            PlanId,
            new AddPlannedTransactionRequest(
                ToDateOnly(_plannedTransactionForm.Date),
                _plannedTransactionForm.Name,
                _plannedTransactionForm.Direction,
                _plannedTransactionForm.Amount ?? 0m),
            CancellationToken.None);

        _plan = updatedPlan;
        _plannedTransactionForm = PlannedTransactionFormModel.CreateDefault();
        Snackbar.Add("Planned transaction added.", Severity.Success);
    }

    private async Task AddOverrideAsync()
    {
        var updatedPlan = await BudgetPlanService.AddOverrideAsync(
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

        _plan = updatedPlan;
        _overrideForm = OccurrenceOverrideFormModel.CreateDefault();
        Snackbar.Add("Override added.", Severity.Success);
    }

    private async Task RunForecastAsync()
    {
        _forecastResult = await BudgetPlanService.ForecastAsync(
            PlanId,
            new ForecastRequest(
                ToDateOnly(_forecastForm.StartDate),
                ToDateOnly(_forecastForm.EndDate)),
            CancellationToken.None);

        Snackbar.Add("Forecast calculated.", Severity.Success);
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
                .Select(rule => new SourceOption(rule.RuleId.ToString(), rule.Name))
                .ToList(),
            OccurrenceSource.PlannedTransaction => _plan.PlannedTransactions
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

    private static string GetBalanceClass(decimal amount)
        => amount < 0m ? "balance-negative" : "balance-positive";
}
