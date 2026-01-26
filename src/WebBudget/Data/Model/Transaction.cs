using System.ComponentModel.DataAnnotations.Schema;

namespace WebBudget.Data.Model;

public class Transaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public Account Account { get; set; } = null!;
}
