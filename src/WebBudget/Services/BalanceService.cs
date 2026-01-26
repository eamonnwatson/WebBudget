using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebBudget.Data;
using WebBudget.Data.Model;

namespace WebBudget.Services;

public class BalanceService : BackgroundService
{
#if DEBUG
    private static readonly TimeSpan timerTime = new(0, 0, 30);
#else
    private static readonly TimeSpan timerTime = new(0, 5, 0);
#endif
    private readonly IServiceScopeFactory factory;
    private readonly ILogger<BalanceService> logger;

    public BalanceService(IServiceScopeFactory factory, ILogger<BalanceService> logger)
    {
        this.factory = factory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(timerTime);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using AsyncServiceScope scope = factory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BudgetService>();

            var account = await service.GetAccount();

            if (account.LastUpdated.Date < DateTime.Today)
            {
                logger.LogInformation("Account updated more than 24 hours ago.");
                var recurringtxns = (await service.GetRecurringTransactions()).Where(t => t.NextDate.GetValueOrDefault() == DateTime.Today.ToDateOnly());

                foreach (var item in recurringtxns)
                {
                    logger.LogInformation("New Txn: {Description} ({Amount})", item.Description, item.Amount);

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
