using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

/// <summary>
/// Aggregates the balance checkpoint, recurring rules, one-off transactions, and overrides for a plan.
/// </summary>
public sealed class BudgetPlan
{
    public Guid PlanId { get; }
    public string Name { get; private set; }
    public string Currency { get; }
    public Money StartingBalance { get; private set; }
    public DateOnly BalanceAsOfDate { get; private set; }
    public string TimeZoneId { get; private set; }

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

    public void UpdateRecurringRule(
        Guid ruleId,
        string name,
        TransactionDirection direction,
        Money amount,
        DateOnly effectiveStartDate,
        DateOnly? effectiveEndDate,
        RecurrenceRule recurrence,
        bool isActive,
        int? defaultAlertDaysBefore)
    {
        if (amount.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");

        var rule = _recurringRules.FirstOrDefault(candidate => candidate.RuleId == ruleId)
            ?? throw new InvalidOperationException($"Recurring rule '{ruleId}' was not found.");

        rule.Update(
            name,
            direction,
            amount,
            effectiveStartDate,
            effectiveEndDate,
            recurrence,
            isActive,
            defaultAlertDaysBefore);
    }

    public void RemoveRecurringRule(Guid ruleId)
    {
        var rule = _recurringRules.FirstOrDefault(candidate => candidate.RuleId == ruleId)
            ?? throw new InvalidOperationException($"Recurring rule '{ruleId}' was not found.");

        _recurringRules.Remove(rule);
        // Overrides tied to a deleted source can no longer be applied safely.
        _overrides.RemoveAll(overrideEntry =>
            overrideEntry.Source == OccurrenceSource.RecurringRule &&
            overrideEntry.SourceId == ruleId);
    }

    public void UpdatePlannedTransaction(Guid transactionId, DateOnly date, string name, TransactionDirection direction, Money amount)
    {
        if (amount.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");

        var transaction = _plannedTransactions.FirstOrDefault(candidate => candidate.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Planned transaction '{transactionId}' was not found.");

        transaction.Update(date, name, direction, amount);
    }

    public void RemovePlannedTransaction(Guid transactionId)
    {
        var transaction = _plannedTransactions.FirstOrDefault(candidate => candidate.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Planned transaction '{transactionId}' was not found.");

        _plannedTransactions.Remove(transaction);
        // Keep overrides in sync with the remaining source items.
        _overrides.RemoveAll(overrideEntry =>
            overrideEntry.Source == OccurrenceSource.PlannedTransaction &&
            overrideEntry.SourceId == transactionId);
    }

    public void UpdateOverride(
        Guid overrideId,
        OccurrenceSource source,
        Guid sourceId,
        DateOnly originalDate,
        OverrideAction action,
        DateOnly? newDate,
        Money? newAmount,
        string? newName)
    {
        if (newAmount.HasValue && newAmount.Value.Currency != Currency) throw new InvalidOperationException("Currency mismatch.");

        var overrideEntry = _overrides.FirstOrDefault(candidate => candidate.OverrideId == overrideId)
            ?? throw new InvalidOperationException($"Occurrence override '{overrideId}' was not found.");

        overrideEntry.Update(source, sourceId, originalDate, action, newDate, newAmount, newName);
    }

    public void RemoveOverride(Guid overrideId)
    {
        var overrideEntry = _overrides.FirstOrDefault(candidate => candidate.OverrideId == overrideId)
            ?? throw new InvalidOperationException($"Occurrence override '{overrideId}' was not found.");

        _overrides.Remove(overrideEntry);
    }
}
