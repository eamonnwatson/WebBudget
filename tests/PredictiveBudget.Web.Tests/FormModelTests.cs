using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Web.Features.BudgetPlans.Models;

namespace PredictiveBudget.Web.Tests;

public sealed class FormModelTests
{
    [Fact]
    public void BalanceUpdateFormModel_CreateDefault_UsesProvidedValues()
    {
        var model = BalanceUpdateFormModel.CreateDefault(245.55m, new DateOnly(2026, 3, 20));

        Assert.Equal(245.55m, model.Amount);
        Assert.Equal(new DateTime(2026, 3, 20), model.BalanceAsOfDate);
    }

    [Fact]
    public void CreateBudgetPlanFormModel_CreateDefault_UsesExpectedDefaults()
    {
        var model = CreateBudgetPlanFormModel.CreateDefault();

        Assert.Equal(string.Empty, model.Name);
        Assert.Equal("CAD", model.Currency);
        Assert.Equal(0m, model.StartingBalance);
        Assert.Equal(TimeZoneInfo.Local.Id, model.TimeZoneId);
    }

    [Fact]
    public void ForecastFormModel_CreateDefault_StartsWithNinetyDayWindow()
    {
        var model = ForecastFormModel.CreateDefault();

        Assert.Equal(DateTime.Today, model.StartDate);
        Assert.Equal(DateTime.Today.AddDays(90), model.EndDate);
    }

    [Fact]
    public void OccurrenceOverrideFormModel_CreateDefault_UsesSkipAction()
    {
        var model = OccurrenceOverrideFormModel.CreateDefault();

        Assert.Equal(OccurrenceSource.RecurringRule, model.Source);
        Assert.Equal(OverrideAction.Skip, model.Action);
        Assert.Equal(DateTime.Today, model.OriginalDate);
    }

    [Fact]
    public void PlannedTransactionFormModel_CreateDefault_UsesOutflowDefaults()
    {
        var model = PlannedTransactionFormModel.CreateDefault();

        Assert.Equal(TransactionDirection.Outflow, model.Direction);
        Assert.Equal(0m, model.Amount);
        Assert.Equal(DateTime.Today, model.Date);
    }

    [Fact]
    public void RecurringRuleFormModel_CreateDefault_SeedsTodaySelections()
    {
        var model = RecurringRuleFormModel.CreateDefault();

        Assert.Equal(RecurrencePattern.Weekly, model.Pattern);
        Assert.Contains(model.SelectedMonths, month => month == DateTime.Today.Month);
        Assert.Single(model.SelectedWeekdays);
    }

    [Fact]
    public void SourceOption_Record_PreservesValues()
    {
        var option = new SourceOption("abc", "Salary");

        Assert.Equal("abc", option.Id);
        Assert.Equal("Salary", option.Label);
    }
}
