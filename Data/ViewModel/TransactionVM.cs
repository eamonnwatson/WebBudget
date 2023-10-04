namespace WebBudget.Data.ViewModel;

public class TransactionVM
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
}
