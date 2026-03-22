using MudBlazor;
using NSubstitute;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Web.Features.BudgetPlans.Models;
using PredictiveBudget.Web.Features.BudgetPlans.Workspace;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

/// <summary>
/// Verifies the detailed workspace component for editing rules, transactions, and overrides.
/// </summary>
public sealed class PlanDetailsTests
{
    [Fact]
    public async Task OnParametersSetAsync_LoadsPlanAndSeedsBalanceForm()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = CreateComponent(service, plan.PlanId);

        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");

        var loadedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan?>(component, "_plan");
        var balanceForm = ReflectionTestHelper.GetPrivateField<BalanceUpdateFormModel>(component, "_balanceForm");
        Assert.NotNull(loadedPlan);
        Assert.Equal(125m, balanceForm.Amount);
        Assert.Equal(new DateTime(2026, 3, 20), balanceForm.BalanceAsOfDate);
        Assert.False(string.IsNullOrWhiteSpace(loadedPlan.CalendarSubscriptionToken));
        Assert.Equal(
            $"http://localhost/subscriptions/plans/{loadedPlan.PlanId}/{loadedPlan.CalendarSubscriptionToken}.ics",
            ReflectionTestHelper.InvokeInstance<string?>(component, "GetCalendarSubscriptionUrl"));
    }

    [Fact]
    public async Task UpdateBalanceAsync_UpdatesPlanState()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");
        ReflectionTestHelper.SetPrivateField(component, "_balanceForm", BalanceUpdateFormModel.CreateDefault(300m, new DateOnly(2026, 3, 25)));

        await ReflectionTestHelper.InvokeAsync(component, "UpdateBalanceAsync");

        var updatedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_plan");
        Assert.Equal(300m, updatedPlan.StartingBalance.Amount);
        Assert.Equal(new DateOnly(2026, 3, 25), updatedPlan.BalanceAsOfDate);
    }

    [Fact]
    public async Task AddRecurringRuleAsync_AddsRuleAndResetsForm()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");
        var form = new RecurringRuleFormModel
        {
            Name = "Payday",
            Direction = TransactionDirection.Inflow,
            Amount = 1000m,
            EffectiveStartDate = new DateTime(2026, 3, 20),
            Pattern = RecurrencePattern.Weekly,
            IntervalWeeks = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.None,
            IsActive = true
        };
        form.SelectedWeekdays.Clear();
        form.SelectedWeekdays.Add(Weekday.Friday);
        ReflectionTestHelper.SetPrivateField(component, "_recurringRuleForm", form);

        await ReflectionTestHelper.InvokeAsync(component, "AddRecurringRuleAsync");

        var updatedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_plan");
        Assert.Single(updatedPlan.RecurringRules);
        Assert.Equal(string.Empty, ReflectionTestHelper.GetPrivateField<RecurringRuleFormModel>(component, "_recurringRuleForm").Name);
    }

    [Fact]
    public async Task OpenEditRecurringRuleModal_LoadsExistingRuleIntoForm()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var seededPlan = await service.AddRecurringRuleAsync(
            plan.PlanId,
            new AddRecurringRuleRequest(
                "Payday",
                TransactionDirection.Inflow,
                1000m,
                new DateOnly(2026, 3, 20),
                null,
                RecurrencePattern.Weekly,
                1,
                [Weekday.Friday],
                1,
                [],
                20,
                BusinessDayAdjustment.None,
                true,
                3),
            CancellationToken.None);
        var ruleId = seededPlan.RecurringRules.Single().RuleId;
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");

        ReflectionTestHelper.InvokeVoid(component, "OpenEditRecurringRuleModal", ruleId);

        var form = ReflectionTestHelper.GetPrivateField<RecurringRuleFormModel>(component, "_recurringRuleForm");
        Assert.True(ReflectionTestHelper.GetPrivateField<bool>(component, "_showRecurringRuleModal"));
        Assert.Equal("Payday", form.Name);
        Assert.Equal(TransactionDirection.Inflow, form.Direction);
        Assert.Equal(1000m, form.Amount);
    }

    [Fact]
    public async Task AddPlannedTransactionAsync_AddsTransactionAndResetsForm()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");
        ReflectionTestHelper.SetPrivateField(component, "_plannedTransactionForm", new PlannedTransactionFormModel
        {
            Name = "Rent",
            Date = new DateTime(2026, 3, 21),
            Amount = 40m,
            Direction = TransactionDirection.Outflow
        });

        await ReflectionTestHelper.InvokeAsync(component, "AddPlannedTransactionAsync");

        var updatedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_plan");
        Assert.Single(updatedPlan.PlannedTransactions);
        Assert.Equal(string.Empty, ReflectionTestHelper.GetPrivateField<PlannedTransactionFormModel>(component, "_plannedTransactionForm").Name);
    }

    [Fact]
    public async Task ConfirmDeleteAsync_RemovesPlannedTransactionAfterConfirmation()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var seededPlan = await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 40m),
            CancellationToken.None);
        var transactionId = seededPlan.PlannedTransactions.Single().TransactionId;
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");

        ReflectionTestHelper.InvokeVoid(component, "OpenDeletePlannedTransactionConfirmation", transactionId, "Rent");
        await ReflectionTestHelper.InvokeAsync(component, "ConfirmDeleteAsync");

        var updatedPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_plan");
        Assert.Empty(updatedPlan.PlannedTransactions);
        Assert.False(ReflectionTestHelper.GetPrivateField<bool>(component, "_showDeleteModal"));
    }

    [Fact]
    public async Task AddOverrideAsync_AddsOverrideAndResetsForm()
    {
        var context = new WebBudgetPlanContext();
        var service = context.CreateService();
        var plan = await service.CreateAsync(
            new CreateBudgetPlanRequest("Household", "CAD", 125m, new DateOnly(2026, 3, 20), "America/Halifax"),
            CancellationToken.None);
        var updatedPlan = await service.AddPlannedTransactionAsync(
            plan.PlanId,
            new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 40m),
            CancellationToken.None);
        var transactionId = updatedPlan.PlannedTransactions.Single().TransactionId;
        var component = CreateComponent(service, plan.PlanId);
        await ReflectionTestHelper.InvokeAsync(component, "OnParametersSetAsync");
        ReflectionTestHelper.SetPrivateField(component, "_overrideForm", new OccurrenceOverrideFormModel
        {
            Source = OccurrenceSource.PlannedTransaction,
            SourceId = transactionId.ToString(),
            OriginalDate = new DateTime(2026, 3, 21),
            Action = OverrideAction.ReplaceName,
            NewName = "Moved rent"
        });

        await ReflectionTestHelper.InvokeAsync(component, "AddOverrideAsync");

        var latestPlan = ReflectionTestHelper.GetPrivateField<BudgetPlan>(component, "_plan");
        Assert.Single(latestPlan.Overrides);
        Assert.Equal(string.Empty, ReflectionTestHelper.GetPrivateField<OccurrenceOverrideFormModel>(component, "_overrideForm").SourceId);
    }

    [Fact]
    public void HelperMethods_ReturnDisplayFriendlyValues()
    {
        var planId = Guid.NewGuid();
        var recurringRuleId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var plan = new BudgetPlan(
            planId,
            "Household",
            "CAD",
            new Money(100m, "CAD"),
            new DateOnly(2026, 3, 20),
            "America/Halifax");
        plan.AddRecurringRule(new RecurringTransactionRule(
            recurringRuleId,
            planId,
            "Payday",
            TransactionDirection.Inflow,
            new Money(1000m, "CAD"),
            new DateOnly(2026, 3, 20),
            null,
            new WeeklyRecurrence(1, new HashSet<Weekday> { Weekday.Friday })));
        plan.AddPlannedTransaction(new PlannedTransaction(
            transactionId,
            planId,
            new DateOnly(2026, 3, 21),
            "Rent",
            TransactionDirection.Outflow,
            new Money(40m, "CAD")));
        var component = new PlanDetails();
        ReflectionTestHelper.SetPrivateField(component, "_plan", plan);
        ReflectionTestHelper.SetPrivateField(component, "_recurringRuleForm", RecurringRuleFormModel.CreateDefault());

        var recurrenceText = ReflectionTestHelper.InvokeStatic<string>(typeof(PlanDetails), "DescribeRecurrence", plan.RecurringRules.Single().Recurrence);
        var windowText = ReflectionTestHelper.InvokeStatic<string>(typeof(PlanDetails), "DescribeEffectiveWindow", plan.RecurringRules.Single());
        var overrideText = ReflectionTestHelper.InvokeStatic<string>(
            typeof(PlanDetails),
            "DescribeOverride",
            new OccurrenceOverride(Guid.NewGuid(), planId, OccurrenceSource.PlannedTransaction, transactionId, new DateOnly(2026, 3, 21), OverrideAction.MoveToDate, newDate: new DateOnly(2026, 3, 25)));
        var sourceOptions = ReflectionTestHelper.InvokeInstance<IReadOnlyList<SourceOption>>(component, "GetSourceOptions", OccurrenceSource.PlannedTransaction);
        var sourceLabel = ReflectionTestHelper.InvokeInstance<string>(
            component,
            "GetOverrideSourceLabel",
            new OccurrenceOverride(Guid.NewGuid(), planId, OccurrenceSource.RecurringRule, recurringRuleId, new DateOnly(2026, 3, 20), OverrideAction.Skip));

        Assert.Equal("Every 1 week(s) on Friday", recurrenceText);
        Assert.Equal("Mar 20, 2026 onward", windowText);
        Assert.Equal("Move to Mar 25, 2026", overrideText);
        Assert.Single(sourceOptions);
        Assert.Equal("Payday", sourceLabel);
    }

    [Fact]
    public void WeekdayAndMonthSelectionMethods_ToggleChoices()
    {
        var component = new PlanDetails();
        var form = RecurringRuleFormModel.CreateDefault();
        form.SelectedWeekdays.Clear();
        form.SelectedMonths.Clear();
        ReflectionTestHelper.SetPrivateField(component, "_recurringRuleForm", form);

        ReflectionTestHelper.InvokeVoid(component, "SetWeekday", Weekday.Friday, true);
        ReflectionTestHelper.InvokeVoid(component, "SetMonth", 12, true);

        var updatedForm = ReflectionTestHelper.GetPrivateField<RecurringRuleFormModel>(component, "_recurringRuleForm");
        Assert.Contains(Weekday.Friday, updatedForm.SelectedWeekdays);
        Assert.Contains(12, updatedForm.SelectedMonths);
    }

    private static PlanDetails CreateComponent(BudgetPlanService service, Guid planId)
    {
        var component = new PlanDetails();
        ReflectionTestHelper.SetProperty(component, nameof(PlanDetails.PlanId), planId);
        ReflectionTestHelper.SetPrivateProperty(component, "BudgetPlanService", service);
        ReflectionTestHelper.SetPrivateProperty(component, "NavigationManager", new TestNavigationManager());
        ReflectionTestHelper.SetPrivateProperty(component, "Snackbar", Substitute.For<ISnackbar>());
        return component;
    }
}
