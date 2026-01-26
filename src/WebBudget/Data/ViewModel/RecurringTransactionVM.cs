using System.ComponentModel.DataAnnotations;
using WebBudget.Data.Model;
using WebBudget.Validator;

namespace WebBudget.Data.ViewModel;

public class RecurringTransactionVM
{
    public int Id { get; set; }
    [Required]
    [StringLength(30, ErrorMessage = "Description length can't be more than 30.")]
    public string Description { get; set; } = string.Empty;
    [Required]
    public TransactionType TransactionType { get; set; }
    [Required] 
    public DateTime? StartDate { get; set; }
    [Required]
    [AmountValidator(ErrorMessage = "Amount cannot be 0")]
    public decimal Amount { get; set; }
    public DateOnly? NextDate { get; set; }

}
