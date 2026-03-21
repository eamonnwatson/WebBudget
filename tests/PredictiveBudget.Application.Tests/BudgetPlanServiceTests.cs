using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Application.Tests.TestSupport;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Application.Tests;

public sealed class BudgetPlanServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsPlansFromRepository()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var first = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 1200m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var second = await service.CreateAsync(
            new CreateBudgetPlanRequest("Travel", "USD", 300m, new DateOnly(2026, 3, 21), "America/New_York"),
            CancellationToken.None);

        var plans = await service.ListAsync(CancellationToken.None);

        Assert.Equal(2, plans.Count);
        Assert.Contains(plans, plan => plan.PlanId == first.PlanId);
        Assert.Contains(plans, plan => plan.PlanId == second.PlanId);
    }

    [Fact]
    public async Task GetAsync_ReturnsMatchingPlan()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 1250m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var loaded = await service.GetAsync(plan.PlanId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(plan.PlanId, loaded.PlanId);
    }

    [Fact]
    public async Task CreateAsync_PersistsPlanWithNormalizedCurrency()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();

        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", " cad ", 1250m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        Assert.Equal("Household", plan.Name);
        Assert.Equal("CAD", plan.Currency);
        Assert.Equal(1250m, plan.StartingBalance.Amount);
        Assert.Single(context.Repository.Plans);
    }

    [Fact]
    public async Task CreateAsync_UsesClockDateAndLocalTimeZoneWhenRequestOmitsThem()
    {
        var today = new DateOnly(2026, 4, 1);
        var context = new ApplicationTestContext(today);
        var service = context.CreateService();

        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "usd", 25m, null, "   "),
            CancellationToken.None);

        Assert.Equal(today, plan.BalanceAsOfDate);
        Assert.Equal(TimeZoneInfo.Local.Id, plan.TimeZoneId);
        Assert.Equal("USD", plan.Currency);
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankName()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateBudgetPlanRequest("  ", "CAD", 0m, null, null),
            CancellationToken.None));

        Assert.Equal("Name is required.", error.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsTooLongCurrency()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "ABCDEFGHIJKLM", 0m, null, null),
            CancellationToken.None));

        Assert.Equal("Currency code must be 12 characters or fewer.", error.Message);
    }

    [Fact]
    public async Task UpdateStartingBalanceAsync_UpdatesAmountAndDate()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var updated = await service.UpdateStartingBalanceAsync(
            plan.PlanId,
            new UpdateStartingBalanceRequest(245.55m, new DateOnly(2026, 3, 25)),
            CancellationToken.None);

        Assert.Equal(245.55m, updated.StartingBalance.Amount);
        Assert.Equal(new DateOnly(2026, 3, 25), updated.BalanceAsOfDate);
    }

    [Fact]
    public async Task UpdateStartingBalanceAsync_ThrowsWhenPlanDoesNotExist()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var missingId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateStartingBalanceAsync(
            missingId,
            new UpdateStartingBalanceRequest(50m, new DateOnly(2026, 3, 20)),
            CancellationToken.None));

        Assert.Equal($"Budget plan '{missingId}' was not found.", error.Message);
    }

    [Fact]
    public async Task AddRecurringRuleAsync_AddsWeeklyRuleToPlan()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Salary", "CAD", 0m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var updated = await service.AddRecurringRuleAsync(
            plan.PlanId,
            new AddRecurringRuleRequest(
                "Payday",
                TransactionDirection.Inflow,
                2000m,
                new DateOnly(2026, 3, 20),
                null,
                RecurrencePattern.Weekly,
                2,
                [Weekday.Friday],
                1,
                [],
                20,
                BusinessDayAdjustment.None,
                true,
                3),
            CancellationToken.None);

        var rule = Assert.Single(updated.RecurringRules);
        Assert.Equal("Payday", rule.Name);
        var weekly = Assert.IsType<WeeklyRecurrence>(rule.Recurrence);
        Assert.Equal(2, weekly.IntervalWeeks);
        Assert.Contains(Weekday.Friday, weekly.Weekdays);
    }

    [Fact]
    public async Task AddRecurringRuleAsync_CreatesMonthlyRule()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 0m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var updated = await service.AddRecurringRuleAsync(
            plan.PlanId,
            new AddRecurringRuleRequest(
                "Mortgage",
                TransactionDirection.Outflow,
                900m,
                new DateOnly(2026, 3, 20),
                null,
                RecurrencePattern.MonthlyByDayOfMonth,
                0,
                [],
                3,
                [],
                1,
                BusinessDayAdjustment.NextBusinessDay,
                true,
                null),
            CancellationToken.None);

        var monthly = Assert.IsType<MonthlyByDayOfMonthRecurrence>(Assert.Single(updated.RecurringRules).Recurrence);
        Assert.Equal(3, monthly.IntervalMonths);
        Assert.Equal(1, monthly.DayOfMonth);
    }

    [Fact]
    public async Task AddRecurringRuleAsync_CreatesYearlyRule()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Insurance", "CAD", 0m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var updated = await service.AddRecurringRuleAsync(
            plan.PlanId,
            new AddRecurringRuleRequest(
                "Renewal",
                TransactionDirection.Outflow,
                150m,
                new DateOnly(2026, 3, 20),
                null,
                RecurrencePattern.YearlyByMonthsAndDay,
                0,
                [],
                0,
                [2, 9],
                28,
                BusinessDayAdjustment.PreviousBusinessDay,
                true,
                null),
            CancellationToken.None);

        var yearly = Assert.IsType<YearlyByMonthsAndDayRecurrence>(Assert.Single(updated.RecurringRules).Recurrence);
        Assert.Equal([2, 9], yearly.Months.OrderBy(month => month).ToArray());
        Assert.Equal(28, yearly.DayOfMonth);
    }

    [Fact]
    public async Task AddRecurringRuleAsync_RequiresAtLeastOneWeekdayForWeeklyRules()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Salary", "CAD", 0m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddRecurringRuleAsync(
            plan.PlanId,
            new AddRecurringRuleRequest(
                "Payday",
                TransactionDirection.Inflow,
                2000m,
                new DateOnly(2026, 3, 20),
                null,
                RecurrencePattern.Weekly,
                1,
                [],
                0,
                [],
                20,
                BusinessDayAdjustment.None,
                true,
                null),
            CancellationToken.None));

        Assert.Equal("Choose at least one weekday.", error.Message);
    }

    [Fact]
    public async Task AddPlannedTransactionAsync_TrimsNameAndPersistsTransaction()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var updated = await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), " Rent ", TransactionDirection.Outflow, 100m),
            CancellationToken.None);

        var transaction = Assert.Single(updated.PlannedTransactions);
        Assert.Equal("Rent", transaction.Name);
        Assert.Equal(100m, transaction.Amount.Amount);
    }

    [Fact]
    public async Task AddOverrideAsync_StoresAmountOverrideInPlanCurrency()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var updatedPlan = await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 100m),
            CancellationToken.None);
        var sourceId = Assert.Single(updatedPlan.PlannedTransactions).TransactionId;

        var updated = await service.AddOverrideAsync(
            plan.PlanId,
            new AddOccurrenceOverrideRequest(
                OccurrenceSource.PlannedTransaction,
                sourceId,
                new DateOnly(2026, 3, 21),
                OverrideAction.ReplaceAmount,
                null,
                25m,
                null),
            CancellationToken.None);

        var overrideEntry = Assert.Single(updated.Overrides);
        Assert.Equal(25m, overrideEntry.NewAmount?.Amount);
        Assert.Equal("CAD", overrideEntry.NewAmount?.Currency);
    }

    [Fact]
    public async Task AddOverrideAsync_TrimsReplacementName()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var updatedPlan = await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 100m),
            CancellationToken.None);
        var sourceId = Assert.Single(updatedPlan.PlannedTransactions).TransactionId;

        var updated = await service.AddOverrideAsync(
            plan.PlanId,
            new AddOccurrenceOverrideRequest(
                OccurrenceSource.PlannedTransaction,
                sourceId,
                new DateOnly(2026, 3, 21),
                OverrideAction.ReplaceName,
                null,
                null,
                "  Rent holiday  "),
            CancellationToken.None);

        Assert.Equal("Rent holiday", Assert.Single(updated.Overrides).NewName);
    }

    [Fact]
    public async Task ForecastAsync_ReturnsFirstBelowZeroDateWhenPlanGoesNegative()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 100m),
            CancellationToken.None);

        var forecast = await service.ForecastAsync(
            plan.PlanId,
            new ForecastRequest(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22)),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 3, 21), forecast.Summary.FirstBelowZeroDate);
        Assert.Contains(new DateOnly(2026, 3, 21), forecast.BelowZeroDates);
    }

    [Fact]
    public async Task ForecastAsync_ThrowsWhenEndDatePrecedesStart()
    {
        var context = new ApplicationTestContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Bills", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ForecastAsync(
            plan.PlanId,
            new ForecastRequest(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 20)),
            CancellationToken.None));

        Assert.Equal("Forecast end date must be on or after the start date.", error.Message);
    }
}
