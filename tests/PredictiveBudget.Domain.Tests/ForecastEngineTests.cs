using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Domain.Tests;

/// <summary>
/// Exercises the forecast engine's expansion and balance-rolling behavior.
/// </summary>
public sealed class ForecastEngineTests
{
    [Fact]
    public void Forecast_ReturnsDailyPointsAndSummaryForStaticBalance()
    {
        var plan = CreatePlan();
        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)));

        Assert.Equal(3, result.DailyPoints.Count);
        Assert.All(result.DailyPoints, point => Assert.Equal(100m, point.EndOfDayBalance.Amount));
        Assert.Equal(new Money(100m, "CAD"), result.Summary.MinBalance);
        Assert.Equal(new Money(100m, "CAD"), result.Summary.MaxBalance);
        Assert.Null(result.Summary.FirstBelowZeroDate);
    }

    [Fact]
    public void Forecast_AppliesRecurringTransactionsPlannedTransactionsAndOverrides()
    {
        var plan = CreatePlan();
        var ruleId = Guid.NewGuid();
        var salaryRule = new RecurringTransactionRule(
            ruleId,
            plan.PlanId,
            "Salary",
            TransactionDirection.Inflow,
            new Money(100m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday }));
        var rentId = Guid.NewGuid();
        var rent = new PlannedTransaction(
            rentId,
            plan.PlanId,
            new DateOnly(2026, 3, 21),
            "Rent",
            TransactionDirection.Outflow,
            new Money(40m, "CAD"));
        var movedRentId = Guid.NewGuid();
        var movedRent = new PlannedTransaction(
            movedRentId,
            plan.PlanId,
            new DateOnly(2026, 3, 22),
            "Moved rent",
            TransactionDirection.Outflow,
            new Money(70m, "CAD"));

        plan.AddRecurringRule(salaryRule);
        plan.AddPlannedTransaction(rent);
        plan.AddPlannedTransaction(movedRent);
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.RecurringRule,
            ruleId,
            new DateOnly(2026, 3, 27),
            OverrideAction.Skip));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.PlannedTransaction,
            rentId,
            new DateOnly(2026, 3, 21),
            OverrideAction.ReplaceAmount,
            newAmount: new Money(55m, "CAD")));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.PlannedTransaction,
            movedRentId,
            new DateOnly(2026, 3, 22),
            OverrideAction.MoveToDate,
            newDate: new DateOnly(2026, 3, 24)));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.PlannedTransaction,
            rentId,
            new DateOnly(2026, 3, 21),
            OverrideAction.ReplaceName,
            newName: "Rent adjusted"));

        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 28)));

        Assert.Equal(200m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 20)).EndOfDayBalance.Amount);
        Assert.Equal(145m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 21)).EndOfDayBalance.Amount);
        Assert.Equal(145m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 22)).EndOfDayBalance.Amount);
        Assert.Equal(75m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 24)).EndOfDayBalance.Amount);
        Assert.Equal(75m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 28)).EndOfDayBalance.Amount);
        Assert.Equal(new DateOnly(2026, 3, 24), result.Summary.MinDate);
        Assert.Equal(new Money(200m, "CAD"), result.Summary.MaxBalance);
        Assert.Null(result.Summary.FirstBelowZeroDate);
        Assert.Collection(
            result.Occurrences,
            occurrence =>
            {
                Assert.Equal(new DateOnly(2026, 3, 20), occurrence.Date);
                Assert.Equal("Salary", occurrence.Name);
                Assert.Equal(TransactionDirection.Inflow, occurrence.Direction);
            },
            occurrence =>
            {
                Assert.Equal(new DateOnly(2026, 3, 21), occurrence.Date);
                Assert.Equal("Rent adjusted", occurrence.Name);
                Assert.Equal(TransactionDirection.Outflow, occurrence.Direction);
                Assert.Equal(55m, occurrence.Amount.Amount);
            },
            occurrence =>
            {
                Assert.Equal(new DateOnly(2026, 3, 24), occurrence.Date);
                Assert.Equal("Moved rent", occurrence.Name);
                Assert.Equal(TransactionDirection.Outflow, occurrence.Direction);
                Assert.Equal(70m, occurrence.Amount.Amount);
            });
    }

    [Fact]
    public void Forecast_TracksFirstBelowZeroDate()
    {
        var plan = CreatePlan();
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 21),
            "Large bill",
            TransactionDirection.Outflow,
            new Money(150m, "CAD")));

        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)));

        Assert.Equal(new DateOnly(2026, 3, 21), result.Summary.FirstBelowZeroDate);
        Assert.Equal([new DateOnly(2026, 3, 21), new DateOnly(2026, 3, 22)], result.BelowZeroDates);
    }

    [Fact]
    public void Forecast_RollsForwardBalanceFromBalanceAsOfDateBeforeVisibleRange()
    {
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(100m, "CAD"),
            new DateOnly(2026, 2, 1),
            "America/Halifax");
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 2, 10),
            "Mid-month bill",
            TransactionDirection.Outflow,
            new Money(25m, "CAD")));
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 1),
            "Payday",
            TransactionDirection.Inflow,
            new Money(50m, "CAD")));
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 22),
            "Groceries",
            TransactionDirection.Outflow,
            new Money(10m, "CAD")));

        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 23)));

        Assert.Equal(115m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 22)).EndOfDayBalance.Amount);
        Assert.Equal(115m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 23)).EndOfDayBalance.Amount);
        Assert.Collection(
            result.Occurrences,
            occurrence =>
            {
                Assert.Equal(new DateOnly(2026, 3, 22), occurrence.Date);
                Assert.Equal("Groceries", occurrence.Name);
            });
    }

    [Fact]
    public void Forecast_RollsBackFromBalanceAsOfDateInsideVisibleRange()
    {
        var plan = new BudgetPlan(
            Guid.NewGuid(),
            "Household",
            "CAD",
            new Money(100m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 18),
            "Payday",
            TransactionDirection.Inflow,
            new Money(50m, "CAD")));
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 19),
            "Groceries",
            TransactionDirection.Outflow,
            new Money(20m, "CAD")));
        plan.AddPlannedTransaction(new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            new DateOnly(2026, 3, 20),
            "Rent",
            TransactionDirection.Outflow,
            new Money(15m, "CAD")));

        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 18), new DateOnly(2026, 3, 20)));

        Assert.Equal(120m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 18)).EndOfDayBalance.Amount);
        Assert.Equal(100m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 19)).EndOfDayBalance.Amount);
        Assert.Equal(85m, result.DailyPoints.Single(point => point.Date == new DateOnly(2026, 3, 20)).EndOfDayBalance.Amount);
    }

    [Fact]
    public void Forecast_PreservesOriginalDateAndAlertLeadTimeAcrossOverrides()
    {
        var plan = CreatePlan();
        var recurringRuleId = Guid.NewGuid();
        var plannedTransactionId = Guid.NewGuid();

        plan.AddRecurringRule(new RecurringTransactionRule(
            recurringRuleId,
            plan.PlanId,
            "Payday",
            TransactionDirection.Inflow,
            new Money(100m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday }),
            defaultAlertDaysBefore: 2));
        plan.AddPlannedTransaction(new PlannedTransaction(
            plannedTransactionId,
            plan.PlanId,
            new DateOnly(2026, 3, 21),
            "Rent",
            TransactionDirection.Outflow,
            new Money(40m, "CAD")));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.RecurringRule,
            recurringRuleId,
            new DateOnly(2026, 3, 20),
            OverrideAction.MoveToDate,
            newDate: new DateOnly(2026, 3, 21)));
        plan.AddOverride(new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            OccurrenceSource.RecurringRule,
            recurringRuleId,
            new DateOnly(2026, 3, 20),
            OverrideAction.ReplaceName,
            newName: "Payday moved"));

        var engine = new ForecastEngine();

        var result = engine.Forecast(plan, new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)));

        var movedRecurringOccurrence = Assert.Single(result.Occurrences, occurrence => occurrence.SourceId == recurringRuleId);
        var plannedOccurrence = Assert.Single(result.Occurrences, occurrence => occurrence.SourceId == plannedTransactionId);

        Assert.Equal(new DateOnly(2026, 3, 21), movedRecurringOccurrence.Date);
        Assert.Equal(new DateOnly(2026, 3, 20), movedRecurringOccurrence.OriginalDate);
        Assert.Equal("Payday moved", movedRecurringOccurrence.Name);
        Assert.Equal(2, movedRecurringOccurrence.AlertDaysBefore);
        Assert.Equal(plannedOccurrence.Date, plannedOccurrence.OriginalDate);
        Assert.Equal(1, plannedOccurrence.AlertDaysBefore);
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
