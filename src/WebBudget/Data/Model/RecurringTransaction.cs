using System.ComponentModel.DataAnnotations.Schema;

namespace WebBudget.Data.Model;

public class RecurringTransaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Amount { get; set; }
    
}
