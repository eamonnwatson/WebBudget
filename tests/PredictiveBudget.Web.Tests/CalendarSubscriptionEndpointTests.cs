using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PredictiveBudget.Application.Common;
using PredictiveBudget.Application.Features.BudgetPlans;
using PredictiveBudget.Domain.Common;
using PredictiveBudget.Persistence.Database;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

/// <summary>
/// Verifies the tokenized iCalendar subscription feed exposed by the web app.
/// </summary>
public sealed class CalendarSubscriptionEndpointTests
{
    [Fact]
    public async Task GetCalendar_WithValidToken_ReturnsIcsFeed()
    {
        await using var factory = new TestWebApplicationFactory();
        var seeded = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var plan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
                CancellationToken.None);
            var updatedPlan = await service.AddPlannedTransactionAsync(
                plan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 40m),
                CancellationToken.None);
            updatedPlan = await service.EnsureCalendarSubscriptionTokenAsync(updatedPlan.PlanId, CancellationToken.None);
            return (updatedPlan, updatedPlan.PlannedTransactions.Single().TransactionId);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{seeded.updatedPlan.PlanId}/{seeded.updatedPlan.CalendarSubscriptionToken}.ics", TestContext.Current.CancellationToken);
        var calendar = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unfoldedCalendar = calendar.Replace("\r\n ", string.Empty, StringComparison.Ordinal);
        var expectedUid =
            $"{seeded.updatedPlan.PlanId:N}-PlannedTransaction-{seeded.TransactionId:N}-20260321@predictivebudget.local";

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("BEGIN:VCALENDAR", unfoldedCalendar);
        Assert.Contains("X-WR-CALNAME:Household forecast", unfoldedCalendar);
        Assert.Contains($"UID:{expectedUid}", unfoldedCalendar);
        Assert.Contains("SUMMARY:Rent (-40.00 CAD)", unfoldedCalendar);
        Assert.Contains("DESCRIPTION:Source: Planned transaction", unfoldedCalendar);
        Assert.Contains("60.00 CAD", unfoldedCalendar);
        Assert.Contains("DTSTART;VALUE=DATE:20260321", unfoldedCalendar);
        Assert.Contains("DTEND;VALUE=DATE:20260322", unfoldedCalendar);
        Assert.Contains("BEGIN:VALARM", unfoldedCalendar);
        Assert.Contains("TRIGGER:-P1D", unfoldedCalendar);
        Assert.Contains("END:VCALENDAR", unfoldedCalendar);
    }

    [Fact]
    public async Task GetCalendar_WithInvalidToken_ReturnsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        var plan = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var createdPlan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
                CancellationToken.None);
            return await service.EnsureCalendarSubscriptionTokenAsync(createdPlan.PlanId, CancellationToken.None);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{plan.PlanId}/wrong-token.ics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendar_WithMissingPlan_ReturnsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{Guid.NewGuid()}/missing-token.ics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendar_WithoutOccurrences_ReturnsEmptyValidCalendar()
    {
        await using var factory = new TestWebApplicationFactory();
        var plan = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var createdPlan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Quiet plan", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
                CancellationToken.None);
            return await service.EnsureCalendarSubscriptionTokenAsync(createdPlan.PlanId, CancellationToken.None);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{plan.PlanId}/{plan.CalendarSubscriptionToken}.ics", TestContext.Current.CancellationToken);
        var calendar = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("BEGIN:VCALENDAR", calendar);
        Assert.DoesNotContain("BEGIN:VEVENT", calendar);
        Assert.Contains("END:VCALENDAR", calendar);
    }

    [Fact]
    public async Task GetCalendar_IncludesRecentTransactionsFromThePreviousTenDays()
    {
        await using var factory = new TestWebApplicationFactory();
        var plan = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var createdPlan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Household", "CAD", 200m, new DateOnly(2026, 3, 1), "America/Halifax"),
                CancellationToken.None);
            var updatedPlan = await service.AddPlannedTransactionAsync(
                createdPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 9), "Too old", TransactionDirection.Outflow, 15m),
                CancellationToken.None);
            updatedPlan = await service.AddPlannedTransactionAsync(
                updatedPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 15), "Recent bill", TransactionDirection.Outflow, 25m),
                CancellationToken.None);
            updatedPlan = await service.AddPlannedTransactionAsync(
                updatedPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Upcoming rent", TransactionDirection.Outflow, 40m),
                CancellationToken.None);
            return await service.EnsureCalendarSubscriptionTokenAsync(updatedPlan.PlanId, CancellationToken.None);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{plan.PlanId}/{plan.CalendarSubscriptionToken}.ics", TestContext.Current.CancellationToken);
        var calendar = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n ", string.Empty, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SUMMARY:Recent bill (-25.00 CAD)", calendar);
        Assert.Contains("DTSTART;VALUE=DATE:20260315", calendar);
        Assert.Contains("SUMMARY:Upcoming rent (-40.00 CAD)", calendar);
        Assert.Contains("DTSTART;VALUE=DATE:20260321", calendar);
        Assert.DoesNotContain("SUMMARY:Too old (-15.00 CAD)", calendar);
        Assert.DoesNotContain("DTSTART;VALUE=DATE:20260309", calendar);
    }

    [Fact]
    public async Task GetCalendar_RecentTransactionRunningBalanceStaysAnchoredToTodayCheckpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        var plan = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var createdPlan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Household", "CAD", 100m, new DateOnly(2026, 3, 20), "America/Halifax"),
                CancellationToken.None);
            var updatedPlan = await service.AddPlannedTransactionAsync(
                createdPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 19), "Recent bill", TransactionDirection.Outflow, 25m),
                CancellationToken.None);
            return await service.EnsureCalendarSubscriptionTokenAsync(updatedPlan.PlanId, CancellationToken.None);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{plan.PlanId}/{plan.CalendarSubscriptionToken}.ics", TestContext.Current.CancellationToken);
        var calendar = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n ", string.Empty, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SUMMARY:Recent bill (-25.00 CAD)", calendar);
        Assert.Contains("Projected balance after transaction: 100.00 CAD", calendar);
        Assert.DoesNotContain("Projected balance after transaction: 75.00 CAD", calendar);
    }

    [Fact]
    public async Task GetCalendar_WhenBalanceDropsBelowZero_AddsContiguousBelowZeroEvent()
    {
        await using var factory = new TestWebApplicationFactory();
        var plan = await factory.WithBudgetPlanServiceAsync(async service =>
        {
            var createdPlan = await service.CreateAsync(
                new CreateBudgetPlanRequest("Household", "CAD", 50m, new DateOnly(2026, 3, 20), "America/Halifax"),
                CancellationToken.None);
            var updatedPlan = await service.AddPlannedTransactionAsync(
                createdPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 21), "Rent", TransactionDirection.Outflow, 60m),
                CancellationToken.None);
            updatedPlan = await service.AddPlannedTransactionAsync(
                updatedPlan.PlanId,
                new AddPlannedTransactionRequest(new DateOnly(2026, 3, 24), "Payday", TransactionDirection.Inflow, 20m),
                CancellationToken.None);
            return await service.EnsureCalendarSubscriptionTokenAsync(updatedPlan.PlanId, CancellationToken.None);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/subscriptions/plans/{plan.PlanId}/{plan.CalendarSubscriptionToken}.ics", TestContext.Current.CancellationToken);
        var calendar = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Replace("\r\n ", string.Empty, StringComparison.Ordinal);
        var expectedUid = $"{plan.PlanId:N}-BelowZero-20260321-20260323@predictivebudget.local";

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"UID:{expectedUid}", calendar);
        Assert.Contains("SUMMARY:Projected balance below zero", calendar);
        Assert.Contains("DTSTART;VALUE=DATE:20260321", calendar);
        Assert.Contains("DTEND;VALUE=DATE:20260324", calendar);
        Assert.Contains("Lowest balance in this stretch: -10.00 CAD", calendar);
    }

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly FixedClock fixedClock = new(new DateOnly(2026, 3, 20));
        private readonly InMemoryBudgetPlanRepository repository = new();
        private readonly string testDatabasePath = Path.Combine(Path.GetTempPath(), $"predictivebudget-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<BudgetDbContext>>();
                services.RemoveAll<IBudgetPlanRepository>();
                services.RemoveAll<IClock>();
                services.AddSingleton<IDbContextFactory<BudgetDbContext>>(
                    new TestDbContextFactory(
                        new DbContextOptionsBuilder<BudgetDbContext>()
                            .UseSqlite($"Data Source={testDatabasePath}")
                            .Options));
                services.AddSingleton<IClock>(fixedClock);
                services.AddSingleton<IBudgetPlanRepository>(repository);
            });
        }

        public async Task<T> WithBudgetPlanServiceAsync<T>(Func<BudgetPlanService, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BudgetPlanService>();
            return await action(service);
        }
    }
}
