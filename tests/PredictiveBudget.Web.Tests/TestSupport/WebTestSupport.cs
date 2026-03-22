using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.BudgetPlans;
using PredictiveBudget.Domain.Forecasting;
using PredictiveBudget.Persistence.Database;

namespace PredictiveBudget.Web.Tests.TestSupport;

internal sealed class WebBudgetPlanContext(DateOnly? today = null)
{
    public InMemoryBudgetPlanRepository Repository { get; } = new();

    public BudgetPlanService CreateService()
        => new(Repository, new ForecastEngine(), new FixedClock(today ?? new DateOnly(2026, 3, 20)));
}

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today() => today;
}

internal sealed class InMemoryBudgetPlanRepository : IBudgetPlanRepository
{
    private readonly Dictionary<Guid, BudgetPlan> plans = [];

    public Task<IReadOnlyList<BudgetPlan>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<BudgetPlan>>(plans.Values.ToList());

    public Task<BudgetPlan?> GetAsync(Guid planId, CancellationToken ct)
        => Task.FromResult(plans.TryGetValue(planId, out var plan) ? plan : null);

    public Task SaveAsync(BudgetPlan plan, CancellationToken ct)
    {
        plans[plan.PlanId] = plan;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid planId, CancellationToken ct)
    {
        plans.Remove(planId);
        return Task.CompletedTask;
    }
}

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        Uri = ToAbsoluteUri(uri).ToString();
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        Uri = ToAbsoluteUri(uri).ToString();
    }
}

internal sealed class TestDbContextFactory(DbContextOptions<BudgetDbContext> options) : IDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext() => new(options);

    public Task<BudgetDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

internal static class ReflectionTestHelper
{
    public static async Task InvokeAsync(object target, string methodName, params object?[]? parameters)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        var result = method.Invoke(target, parameters);
        if (result is Task task)
        {
            await task;
        }
    }

    public static T InvokeStatic<T>(Type targetType, string methodName, params object?[]? parameters)
    {
        var method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        return (T)(method.Invoke(null, parameters) ?? throw new InvalidOperationException($"Method '{methodName}' returned null."));
    }

    public static T InvokeInstance<T>(object target, string methodName, params object?[]? parameters)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        return (T)(method.Invoke(target, parameters) ?? throw new InvalidOperationException($"Method '{methodName}' returned null."));
    }

    public static void SetPrivateProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        property.SetValue(target, value);
    }

    public static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Field '{fieldName}' was null."));
    }

    public static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        property.SetValue(target, value);
    }

    public static void InvokeVoid(object target, string methodName, params object?[]? parameters)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        _ = method.Invoke(target, parameters);
    }
}
