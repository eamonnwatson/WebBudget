using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

/// <summary>
/// Stores a single manually scheduled cashflow item.
/// </summary>
public sealed class PlannedTransaction
{
    public Guid TransactionId { get; }
    public Guid PlanId { get; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; }
    public TransactionDirection Direction { get; private set; }
    public Money Amount { get; private set; }

    public PlannedTransaction(Guid transactionId, Guid planId, DateOnly date, string name, TransactionDirection direction, Money amount)
    {
        TransactionId = transactionId;
        PlanId = planId;
        Date = date;
        Name = name;
        Direction = direction;
        Amount = amount;
    }

    public void Update(DateOnly date, string name, TransactionDirection direction, Money amount)
    {
        Date = date;
        Name = name;
        Direction = direction;
        Amount = amount;
    }
}
