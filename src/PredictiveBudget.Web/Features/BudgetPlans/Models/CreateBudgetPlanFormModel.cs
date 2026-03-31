using System.ComponentModel.DataAnnotations;

using PredictiveBudget.Domain.BudgetPlans;

namespace PredictiveBudget.Web.Features.BudgetPlans.Models;

/// <summary>
/// Binds the dashboard modal used to create or edit a budget plan.
/// </summary>
public sealed class CreateBudgetPlanFormModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(12)]
    public string Currency { get; set; } = "CAD";

    [Required]
    [Range(typeof(decimal), "-1000000000", "1000000000")]
    public decimal? StartingBalance { get; set; } = 0m;

    [Required]
    public DateTime? BalanceAsOfDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(100)]
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    public static CreateBudgetPlanFormModel CreateDefault()
        => new();

    public static CreateBudgetPlanFormModel CreateFromPlan(BudgetPlan plan)
        => new()
        {
            Name = plan.Name,
            Currency = plan.Currency,
            StartingBalance = plan.StartingBalance.Amount,
            BalanceAsOfDate = plan.BalanceAsOfDate.ToDateTime(TimeOnly.MinValue),
            TimeZoneId = plan.TimeZoneId
        };
}
