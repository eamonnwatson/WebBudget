using System.ComponentModel.DataAnnotations;

namespace PredictiveBudget.Web.Features.BudgetPlans.Models;

public sealed class ForecastFormModel
{
    [Required]
    public DateTime? StartDate { get; set; } = DateTime.Today;

    [Required]
    public DateTime? EndDate { get; set; } = DateTime.Today.AddDays(90);

    public static ForecastFormModel CreateDefault(DateOnly? startDate = null, int durationDays = 90)
    {
        var start = (startDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);

        return new ForecastFormModel
        {
            StartDate = start,
            EndDate = start.AddDays(durationDays)
        };
    }
}
