using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebBudget.Data;
using WebBudget.Data.Model;

namespace WebBudget.Services;

public class BalanceService : BackgroundService
{
    private static readonly TimeSpan timerTime = new(0, 5, 0);
    private readonly IServiceScopeFactory factory;

    public BalanceService(IServiceScopeFactory factory)
    {
        this.factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(timerTime);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using AsyncServiceScope scope = factory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BudgetService>();

            var account = await service.GetAccount();
            
            if ((DateTime.Today - account.LastUpdated).TotalHours > 24)
            {
                var recurringtxns = (await service.GetRecurringTransactions()).Where(t => t.NextDate.GetValueOrDefault() == DateTime.Today.ToDateOnly());

                foreach (var item in recurringtxns)
                {
                    var txn = new Transaction() { Account = account, Amount = item.Amount, Description = item.Description, TransactionDate = DateTime.Today.ToDateOnly() };
                    await service.AddTransaction(txn);
                    account.CurrentBalance += item.Amount;
                    account.LastUpdated = DateTime.Now;
                    await service.UpdateAccount(account);
                }

                account.LastUpdated = DateTime.Now;
                await service.UpdateAccount(account);
            }
        }
    }
}
