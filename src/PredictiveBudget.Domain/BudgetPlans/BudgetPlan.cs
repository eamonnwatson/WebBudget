using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

public sealed class BudgetPlan
{
    public Guid PlanId { get; }
    public string Name { get; private set; }
    public string Currency { get; }
    public Money StartingBalance { get; private set; }
    public DateOnly BalanceAsOfDate { get; private set; }
    public string TimeZoneId { get; private set; } // e.g. "America/Moncton"

    private readonly List<RecurringTransactionRule> _recurringRules = new();
    private readonly List<PlannedTransaction> _plannedTransactions = new();
    private readonly List<OccurrenceOverride> _overrides = new();

    public IReadOnlyList<RecurringTransactionRule> RecurringRules => _recurringRules;
    public IReadOnlyList<PlannedTransaction> PlannedTransactions => _plannedTransactions;
    public IReadOnlyList<OccurrenceOverride> Overrides => _overrides;

    public BudgetPlan(Guid planId, string name, string currency, Money startingBalance, DateOnly balanceAsOfDate, string timeZoneId)
    {
        PlanId = planId;
        Name = name;
        Currency = currency;
        StartingBalance = startingBalance;
        BalanceAsOfDate = balanceAsOfDate;
        TimeZoneId = timeZoneId;
    }

    public void SetStartingBalance(Money balance, DateOnly asOfDate)
    {
        if (balance.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");
        StartingBalance = balance;
        BalanceAsOfDate = asOfDate;
    }

    public void AddRecurringRule(RecurringTransactionRule rule)
    {
        if (rule.PlanId != PlanId) throw new InvalidOperationException("Rule does not belong to this plan.");
        if (rule.Amount.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");
        _recurringRules.Add(rule);
    }

    public void AddPlannedTransaction(PlannedTransaction txn)
    {
        if (txn.PlanId != PlanId) throw new InvalidOperationException("Transaction does not belong to this plan.");
        if (txn.Amount.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");
        _plannedTransactions.Add(txn);
    }

    public void AddOverride(OccurrenceOverride ov)
    {
        if (ov.PlanId != PlanId) throw new InvalidOperationException("Override does not belong to this plan.");
        _overrides.Add(ov);
    }
}
