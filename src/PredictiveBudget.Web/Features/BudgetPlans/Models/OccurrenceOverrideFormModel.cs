using System.ComponentModel.DataAnnotations;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Web.Features.BudgetPlans.Models;

public sealed class OccurrenceOverrideFormModel
{
    public OccurrenceSource Source { get; set; } = OccurrenceSource.RecurringRule;

    [Required]
    public string SourceId { get; set; } = string.Empty;

    [Required]
    public DateTime? OriginalDate { get; set; } = DateTime.Today;

    public OverrideAction Action { get; set; } = OverrideAction.Skip;

    public DateTime? NewDate { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? NewAmount { get; set; }

    [StringLength(100)]
    public string? NewName { get; set; }

    public static OccurrenceOverrideFormModel CreateDefault()
        => new();
}
