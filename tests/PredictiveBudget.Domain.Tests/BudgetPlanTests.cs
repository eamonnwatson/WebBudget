using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Tests;

public sealed class BudgetPlanTests
{
    [Fact]
    public void SetStartingBalance_UpdatesBalanceWhenCurrencyMatches()
    {
        var plan = CreatePlan();

        plan.SetStartingBalance(new Money(250m, "CAD"), new DateOnly(2026, 3, 25));

        Assert.Equal(250m, plan.StartingBalance.Amount);
        Assert.Equal(new DateOnly(2026, 3, 25), plan.BalanceAsOfDate);
    }

    [Fact]
    public void SetStartingBalance_ThrowsWhenCurrencyDoesNotMatch()
    {
        var plan = CreatePlan();

        var error = Assert.Throws<InvalidOperationException>(() => plan.SetStartingBalance(
            new Money(250m, "USD"),
            new DateOnly(2026, 3, 25)));

        Assert.Equal("Currency mismatch.", error.Message);
    }

    [Fact]
    public void AddRecurringRule_AddsRuleWhenPlanAndCurrencyMatch()
    {
        var plan = CreatePlan();
        var rule = new RecurringTransactionRule(
            Guid.NewGuid(),
            plan.PlanId,
            "Payday",
            TransactionDirection.Inflow,
            new Money(1000m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday }));

        plan.AddRecurringRule(rule);

        Assert.Contains(rule, plan.RecurringRules);
    }

    [Fact]
    public void AddRecurringRule_ThrowsWhenRuleBelongsToAnotherPlan()
    {
        var plan = CreatePlan();
        var rule = new RecurringTransactionRule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Payday",
            TransactionDirection.Inflow,
            new Money(1000m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday }));

        var error = Assert.Throws<InvalidOperationException>(() => plan.AddRecurringRule(rule));

        Assert.Equal("Rule does not belong to this plan.", error.Message);
    }

    [Fact]
    public void AddPlannedTransaction_ThrowsWhenCurrencyDoesNotMatch()
    {
        var plan = CreatePlan();
        var transaction = new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 21),
            "Rent",
            TransactionDirection.Outflow,
            new Money(100m, "USD"));

        var error = Assert.Throws<InvalidOperationException>(() => plan.AddPlannedTransaction(transaction));

        Assert.Equal("Currency mismatch.", error.Message);
    }

    [Fact]
    public void AddOverride_ThrowsWhenOverrideBelongsToAnotherPlan()
    {
        var plan = CreatePlan();
        var overrideEntry = new OccurrenceOverride(
            Guid.NewGuid(),
            Guid.NewGuid(),
            OccurrenceSource.PlannedTransaction,
            Guid.NewGuid(),
            new DateOnly(2026, 3, 21),
            OverrideAction.Skip);

        var error = Assert.Throws<InvalidOperationException>(() => plan.AddOverride(overrideEntry));

        Assert.Equal("Override does not belong to this plan.", error.Message);
    }

    [Fact]
    public void RecurringTransactionRule_IsEffectiveOn_RequiresActiveAndWithinWindow()
    {
        var rule = new RecurringTransactionRule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Payday",
            TransactionDirection.Inflow,
            new Money(1000m, "CAD"),
            new DateOnly(2026, 3, 20),
            new DateOnly(2026, 4, 3),
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday }),
            isActive: true);

        Assert.True(rule.IsEffectiveOn(new DateOnly(2026, 3, 27)));
        Assert.False(rule.IsEffectiveOn(new DateOnly(2026, 3, 19)));
        Assert.False(rule.IsEffectiveOn(new DateOnly(2026, 4, 10)));
    }

    private static BudgetPlan CreatePlan()
        => new(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(100m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
}
