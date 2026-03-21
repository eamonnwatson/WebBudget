using PredictiveBudget.Application.Common;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans.Recurrence;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Application.Features.BudgetPlans;

public sealed class BudgetPlanService(
    IBudgetPlanRepository repository,
    IForecastEngine forecastEngine,
    IClock clock)
{
    public Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct)
        => repository.ListAsync(ct);

    public Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct)
        => repository.GetAsync(planId, ct);

    public async Task<BudgetPlan> CreateAsync(CreateBudgetPlanRequest request, CancellationToken ct)
    {
        string name = NormalizeRequired(request.Name, nameof(request.Name));
        string currency = NormalizeCurrency(request.Currency);
        string timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? TimeZoneInfo.Local.Id
            : request.TimeZoneId.Trim();

        var plan = new BudgetPlan(
            Guid.NewGuid(),
            name,
            currency,
            new Money(request.StartingBalance, currency),
            request.BalanceAsOfDate ?? clock.Today(),
            timeZoneId);

        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> UpdateStartingBalanceAsync(Guid planId, UpdateStartingBalanceRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);
        plan.SetStartingBalance(new Money(request.Amount, plan.Currency), request.BalanceAsOfDate);
        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> AddRecurringRuleAsync(Guid planId, AddRecurringRuleRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        var rule = new RecurringTransactionRule(
            Guid.NewGuid(),
            plan.PlanId,
            NormalizeRequired(request.Name, nameof(request.Name)),
            request.Direction,
            new Money(request.Amount, plan.Currency),
            request.EffectiveStartDate,
            request.EffectiveEndDate,
            BuildRecurrence(
                request.Pattern,
                request.IntervalWeeks,
                request.Weekdays,
                request.IntervalMonths,
                request.Months,
                request.DayOfMonth,
                request.BusinessDayAdjustment),
            request.IsActive,
            request.DefaultAlertDaysBefore);

        plan.AddRecurringRule(rule);
        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> UpdateRecurringRuleAsync(Guid planId, Guid ruleId, UpdateRecurringRuleRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        plan.UpdateRecurringRule(
            ruleId,
            NormalizeRequired(request.Name, nameof(request.Name)),
            request.Direction,
            new Money(request.Amount, plan.Currency),
            request.EffectiveStartDate,
            request.EffectiveEndDate,
            BuildRecurrence(
                request.Pattern,
                request.IntervalWeeks,
                request.Weekdays,
                request.IntervalMonths,
                request.Months,
                request.DayOfMonth,
                request.BusinessDayAdjustment),
            request.IsActive,
            request.DefaultAlertDaysBefore);

        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> AddPlannedTransactionAsync(Guid planId, AddPlannedTransactionRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        var transaction = new PlannedTransaction(
            Guid.NewGuid(),
            plan.PlanId,
            request.Date,
            NormalizeRequired(request.Name, nameof(request.Name)),
            request.Direction,
            new Money(request.Amount, plan.Currency));

        plan.AddPlannedTransaction(transaction);
        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> UpdatePlannedTransactionAsync(Guid planId, Guid transactionId, UpdatePlannedTransactionRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        plan.UpdatePlannedTransaction(
            transactionId,
            request.Date,
            NormalizeRequired(request.Name, nameof(request.Name)),
            request.Direction,
            new Money(request.Amount, plan.Currency));

        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> AddOverrideAsync(Guid planId, AddOccurrenceOverrideRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        var overrideEntry = new OccurrenceOverride(
            Guid.NewGuid(),
            plan.PlanId,
            request.Source,
            request.SourceId,
            request.OriginalDate,
            request.Action,
            request.Action == OverrideAction.MoveToDate ? request.NewDate : null,
            request.Action == OverrideAction.ReplaceAmount && request.NewAmount.HasValue
                ? new Money(request.NewAmount.Value, plan.Currency)
                : null,
            request.Action == OverrideAction.ReplaceName ? NormalizeOptional(request.NewName) : null);

        plan.AddOverride(overrideEntry);
        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<BudgetPlan> UpdateOverrideAsync(Guid planId, Guid overrideId, UpdateOccurrenceOverrideRequest request, CancellationToken ct)
    {
        var plan = await RequirePlanAsync(planId, ct);

        plan.UpdateOverride(
            overrideId,
            request.Source,
            request.SourceId,
            request.OriginalDate,
            request.Action,
            request.Action == OverrideAction.MoveToDate ? request.NewDate : null,
            request.Action == OverrideAction.ReplaceAmount && request.NewAmount.HasValue
                ? new Money(request.NewAmount.Value, plan.Currency)
                : null,
            request.Action == OverrideAction.ReplaceName ? NormalizeOptional(request.NewName) : null);

        await repository.SaveAsync(plan, ct);
        return plan;
    }

    public async Task<ForecastResult> ForecastAsync(Guid planId, ForecastRequest request, CancellationToken ct)
    {
        if (request.End < request.Start)
            throw new InvalidOperationException("Forecast end date must be on or after the start date.");

        var plan = await RequirePlanAsync(planId, ct);
        return forecastEngine.Forecast(plan, new DateRange(request.Start, request.End));
    }

    private async Task<BudgetPlan> RequirePlanAsync(Guid planId, CancellationToken ct)
        => await repository.GetAsync(planId, ct)
           ?? throw new InvalidOperationException($"Budget plan '{planId}' was not found.");

    private static RecurrenceRule BuildRecurrence(
        RecurrencePattern pattern,
        int intervalWeeks,
        IReadOnlyCollection<Weekday> weekdays,
        int intervalMonths,
        IReadOnlyCollection<int> months,
        int dayOfMonth,
        BusinessDayAdjustment businessDayAdjustment)
        => pattern switch
        {
            RecurrencePattern.Weekly => new WeeklyRecurrence(
                Math.Max(1, intervalWeeks),
                weekdays.Count > 0 ? weekdays.ToHashSet() : throw new InvalidOperationException("Choose at least one weekday."),
                businessDayAdjustment),
            RecurrencePattern.MonthlyByDayOfMonth => new MonthlyByDayOfMonthRecurrence(
                Math.Max(1, intervalMonths),
                dayOfMonth,
                businessDayAdjustment),
            RecurrencePattern.YearlyByMonthsAndDay => new YearlyByMonthsAndDayRecurrence(
                months.Count > 0 ? months.ToHashSet() : throw new InvalidOperationException("Choose at least one month."),
                dayOfMonth,
                businessDayAdjustment),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };

    private static string NormalizeCurrency(string currency)
    {
        var value = NormalizeRequired(currency, nameof(currency)).ToUpperInvariant();
        if (value.Length > 12)
            throw new InvalidOperationException("Currency code must be 12 characters or fewer.");

        return value;
    }

    private static string NormalizeRequired(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{paramName} is required.")
            : value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
