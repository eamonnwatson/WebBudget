namespace WebBudget.Data.ViewModel;

public class BudgetVM
{
    public decimal CurrentBalance { get; set; }
    public List<TransactionVM> TransactionList { get; set; } = new List<TransactionVM>();
    public List<TransactionVM> TransactionSummary { get; set; } = new List<TransactionVM>();
}

