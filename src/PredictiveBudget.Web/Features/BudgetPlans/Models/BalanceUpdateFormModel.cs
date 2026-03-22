using System.ComponentModel.DataAnnotations;

namespace PredictiveBudget.Web.Features.BudgetPlans.Models;

public sealed class BalanceUpdateFormModel
{
    [Required]
    [Range(typeof(decimal), "-1000000000", "1000000000")]
    public decimal? Amount { get; set; }

    [Required]
    public DateTime? BalanceAsOfDate { get; set; }

    public static BalanceUpdateFormModel CreateDefault(decimal amount, DateOnly asOfDate)
        => new()
        {
            Amount = amount,
            BalanceAsOfDate = asOfDate.ToDateTime(TimeOnly.MinValue)
        };
}
