using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Persistence.Mapping;

namespace PredictiveBudget.Web.Tests;

public sealed class BudgetPlanMapperTests
{
    [Fact]
    public void ToDocument_AndToDomain_RoundTripPreservesPlanShape()
    {
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(150m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        var weeklyRuleId = Guid.NewGuid();
        var monthlyRuleId = Guid.NewGuid();
        var yearlyRuleId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        plan.AddRecurringRule(new RecurringTransactionRule(
            weeklyRuleId,
            plan.PlanId,
            "Payday",
            TransactionDirection.Inflow,
            new Money(1000m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(2, new HashSet<Weekday> { Weekday.Friday }),
            defaultAlertDaysBefore: 2));
        plan.AddRecurringRule(new RecurringTransactionRule(
            monthlyRuleId,
            plan.PlanId,
            "Mortgage",
            TransactionDirection.Outflow,
            new Money(900m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new MonthlyByDayOfMonthRecurrence(1, 1, BusinessDayAdjustment.NextBusinessDay)));
        plan.AddRecurringRule(new RecurringTransactionRule(
            yearlyRuleId,
            plan.PlanId,
            "Insurance",
            TransactionDirection.Outflow,
            new Money(200m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new YearlyByMonthsAndDayRecurrence(new HashSet<int> { 2, 9 }, 28)));
        plan.AddPlannedTransaction(new PlannedTransaction(
            transactionId,
            plan.PlanId,
            new DateOnly(2026, 3, 21),
            "Rent",
            TransactionDirection.Outflow,
            new Money(50m, "CAD")));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.PlannedTransaction,
            transactionId,
            new DateOnly(2026, 3, 21),
            OverrideAction.ReplaceAmount,
            newAmount: new Money(65m, "CAD")));

        var document = BudgetPlanMapper.ToDocument(plan);
        var roundTripped = BudgetPlanMapper.ToDomain(document);

        Assert.Equal(plan.PlanId, roundTripped.PlanId);
        Assert.Equal("Household", roundTripped.Name);
        Assert.Equal("CAD", roundTripped.Currency);
        Assert.Equal(3, roundTripped.RecurringRules.Count);
        Assert.Single(roundTripped.PlannedTransactions);
        Assert.Single(roundTripped.Overrides);
        Assert.Contains(roundTripped.RecurringRules, rule => rule.Recurrence is WeeklyRecurrence weekly && weekly.IntervalWeeks == 2);
        Assert.Contains(roundTripped.RecurringRules, rule => rule.Recurrence is MonthlyByDayOfMonthRecurrence monthly && monthly.DayOfMonth == 1);
        Assert.Contains(roundTripped.RecurringRules, rule => rule.Recurrence is YearlyByMonthsAndDayRecurrence yearly && yearly.Months.SetEquals([2, 9]));
        Assert.Equal(65m, roundTripped.Overrides.Single().NewAmount?.Amount);
    }

    [Fact]
    public void ToDocument_ThrowsForUnsupportedRecurrence()
    {
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(150m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        plan.AddRecurringRule(new RecurringTransactionRule(
            Guid.NewGuid(),
            plan.PlanId,
            "Unsupported",
            TransactionDirection.Inflow,
            new Money(1m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new UnsupportedRecurrenceRule()));

        var error = Assert.Throws<InvalidOperationException>(() => BudgetPlanMapper.ToDocument(plan));

        Assert.Equal("Unsupported recurrence type 'UnsupportedRecurrenceRule'.", error.Message);
    }

    private sealed record UnsupportedRecurrenceRule()
        : RecurrenceRule(BusinessDayAdjustment.None)
    {
        public override IEnumerable<DateOnly> Expand(DateOnly from, DateOnly to, DateOnly anchor)
            => [];
    }
}
