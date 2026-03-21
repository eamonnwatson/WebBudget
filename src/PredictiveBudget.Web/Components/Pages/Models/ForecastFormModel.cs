using System.ComponentModel.DataAnnotations;

namespace PredictiveBudget.Web.Components.Pages.Models;

public sealed class ForecastFormModel
{
    [Required]
    public DateTime? StartDate { get; set; } = DateTime.Today;

    [Required]
    public DateTime? EndDate { get; set; } = DateTime.Today.AddDays(90);

    public static ForecastFormModel CreateDefault()
        => new();
}
