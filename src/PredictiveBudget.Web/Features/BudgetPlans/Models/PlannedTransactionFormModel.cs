using System.ComponentModel.DataAnnotations;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Web.Features.BudgetPlans.Models;

/// <summary>
/// Binds the modal used to add or edit a one-off planned transaction.
/// </summary>
public sealed class PlannedTransactionFormModel
{
    [Required]
    public DateTime? Date { get; set; } = DateTime.Today;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public TransactionDirection Direction { get; set; } = TransactionDirection.Outflow;

    [Required]
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? Amount { get; set; } = 0m;

    public static PlannedTransactionFormModel CreateDefault()
        => new();
}
