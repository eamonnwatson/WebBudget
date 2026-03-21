using System.ComponentModel.DataAnnotations;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Web.Components.Pages.Models;

public sealed class RecurringRuleFormModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public TransactionDirection Direction { get; set; } = TransactionDirection.Outflow;

    [Required]
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? Amount { get; set; } = 0m;

    [Required]
    public DateTime? EffectiveStartDate { get; set; } = DateTime.Today;

    public DateTime? EffectiveEndDate { get; set; }

    public RecurrencePattern Pattern { get; set; } = RecurrencePattern.Weekly;

    [Range(1, 52)]
    public int? IntervalWeeks { get; set; } = 1;

    [Range(1, 24)]
    public int? IntervalMonths { get; set; } = 1;

    [Range(1, 31)]
    public int? DayOfMonth { get; set; } = DateTime.Today.Day;

    public BusinessDayAdjustment BusinessDayAdjustment { get; set; } = BusinessDayAdjustment.None;

    public bool IsActive { get; set; } = true;

    [Range(0, 365)]
    public int? DefaultAlertDaysBefore { get; set; }

    public HashSet<Weekday> SelectedWeekdays { get; } = [ToWeekday(DateTime.Today.DayOfWeek)];

    public HashSet<int> SelectedMonths { get; } = [DateTime.Today.Month];

    public static RecurringRuleFormModel CreateDefault()
        => new();

    private static Weekday ToWeekday(DayOfWeek dayOfWeek)
        => dayOfWeek switch
        {
            DayOfWeek.Monday => Weekday.Monday,
            DayOfWeek.Tuesday => Weekday.Tuesday,
            DayOfWeek.Wednesday => Weekday.Wednesday,
            DayOfWeek.Thursday => Weekday.Thursday,
            DayOfWeek.Friday => Weekday.Friday,
            DayOfWeek.Saturday => Weekday.Saturday,
            DayOfWeek.Sunday => Weekday.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
        };
}
