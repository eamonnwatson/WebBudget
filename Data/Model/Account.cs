using System.ComponentModel.DataAnnotations.Schema;

namespace WebBudget.Data.Model;

public class Account
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime LastUpdated { get; set; }
    public ICollection<Transaction> Transactions { get; } = new List<Transaction>();
}
