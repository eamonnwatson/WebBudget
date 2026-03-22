using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

/// <summary>
/// Defines a reusable transaction pattern that expands into forecast occurrences.
/// </summary>
public sealed class RecurringTransactionRule
{
    public Guid RuleId { get; }
    public Guid PlanId { get; }
    public string Name { get; private set; }
    public TransactionDirection Direction { get; private set; }
    public Money Amount { get; private set; }

    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public bool IsActive { get; private set; }

    public RecurrenceRule Recurrence { get; private set; }

    /// <summary>
    /// Stores the default reminder lead time when external calendar syncing is added.
    /// </summary>
    public int? DefaultAlertDaysBefore { get; private set; }

    public RecurringTransactionRule(
        Guid ruleId,
        Guid planId,
        string name,
        TransactionDirection direction,
        Money amount,
        DateOnly effectiveStartDate,
        DateOnly? effectiveEndDate,
        RecurrenceRule recurrence,
        bool isActive = true,
        int? defaultAlertDaysBefore = null)
    {
        RuleId = ruleId;
        PlanId = planId;
        Name = name;
        Direction = direction;
        Amount = amount;
        EffectiveStartDate = effectiveStartDate;
        EffectiveEndDate = effectiveEndDate;
        Recurrence = recurrence;
        IsActive = isActive;
        DefaultAlertDaysBefore = defaultAlertDaysBefore;
    }

    public bool IsEffectiveOn(DateOnly date)
        => IsActive
           && date >= EffectiveStartDate
           && (EffectiveEndDate is null || date <= EffectiveEndDate.Value);

    public void Update(
        string name,
        TransactionDirection direction,
        Money amount,
        DateOnly effectiveStartDate,
        DateOnly? effectiveEndDate,
        RecurrenceRule recurrence,
        bool isActive,
        int? defaultAlertDaysBefore)
    {
        Name = name;
        Direction = direction;
        Amount = amount;
        EffectiveStartDate = effectiveStartDate;
        EffectiveEndDate = effectiveEndDate;
        Recurrence = recurrence;
        IsActive = isActive;
        DefaultAlertDaysBefore = defaultAlertDaysBefore;
    }
}
