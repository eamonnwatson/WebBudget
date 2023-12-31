﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Collections;
using WebBudget.Data;
using WebBudget.Data.Model;
using WebBudget.Data.ViewModel;

namespace WebBudget.Services;

public class BudgetService
{
    private readonly BudgetContext context;
    private readonly ILogger<BudgetService> logger;

    public BudgetService(BudgetContext context, ILogger<BudgetService> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    public async Task<BudgetVM> GetData()
    {
        var vm = new BudgetVM();
        var account = await GetAccount();
        var recTxn = await context.RecurringTransactions.ToListAsync();
        var startDate = DateTime.Today.ToDateOnly().AddMonths(-1);

        var pastTxn = await context.Transactions
            .Where(x => x.TransactionDate >= startDate)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        var pastTxnVMs = pastTxn.Select(t => new TransactionVM() { Amount = t.Amount, Date = t.TransactionDate, Description = t.Description }).OrderBy(t => t.Date).ThenByDescending(t => t.Amount).ThenBy(t => t.Description);
        var futureTxnVMs = new List<TransactionVM>();
        foreach (var item in recTxn)
        {
            futureTxnVMs.AddRange(GetTxns(item).Where(t => t.Date > DateTime.Today.ToDateOnly()));
        }

        futureTxnVMs = futureTxnVMs.OrderBy(t => t.Date)
            .ThenByDescending(t => t.Amount)
            .ThenBy(t => t.Description).ToList();

        vm.CurrentBalance = account.CurrentBalance;
        vm.TransactionList.AddRange(pastTxnVMs);
        vm.TransactionList.Add(new TransactionVM() { Date = DateTime.Today.ToDateOnly(), Amount = 0, Balance = vm.CurrentBalance, Description = "Current Balance"  });
        vm.TransactionList.AddRange(futureTxnVMs);

        UpdateTxnBalance(vm.TransactionList);

        vm.TransactionSummary = GetSummaryData(vm.TransactionList.Where(t => t.Date > DateTime.Today.ToDateOnly()));

        return vm;
    }

    public async Task<Account> GetAccount()
    {
        return await context.Accounts.OrderBy(a => a.Id).FirstAsync();
    }

    public async Task<List<RecurringTransactionVM>> GetRecurringTransactions()
    {
        var data = await context.RecurringTransactions.ToListAsync();

        return data.Select(t => new RecurringTransactionVM() { Amount = t.Amount, Description = t.Description, Id = t.Id, StartDate = t.StartDate.ToDateTime(TimeOnly.MinValue), TransactionType = t.TransactionType, NextDate = GetTxns(t).Where(t => t.Date >= DateTime.Today.ToDateOnly()).MinBy(t => t.Date)?.Date }).ToList();
    }
    public async Task<bool> AddTransaction(Transaction transaction)
    {
        if (transaction == null)
            return false;

        await context.Transactions.AddAsync(transaction);
        await context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> AddTxn(RecurringTransactionVM txn)
    {
        var newTxn = new RecurringTransaction() { Amount = txn.Amount, Description = txn.Description, StartDate = txn.StartDate.GetValueOrDefault().ToDateOnly(), TransactionType = txn.TransactionType };

        await context.RecurringTransactions.AddAsync(newTxn);
        await context.SaveChangesAsync();

        return true;
    }
    public async Task<bool> DeleteTxn(int id)
    {
        var txn = context.RecurringTransactions.Find(id);

        if (txn == null)
            return false;

        context.RecurringTransactions.Remove(txn);
        await context.SaveChangesAsync();

        return true;
    }
    public async Task<bool> UpdateAccount(Account account)
    {
        if (account == null) return false;
        context.Accounts.Update(account);
        await context.SaveChangesAsync();
        return true;
    }
    private static List<TransactionVM> GetSummaryData(IEnumerable<TransactionVM> txns)
    {
        var output = new List<TransactionVM>();

        if (!txns.Any())
            return output;

        var nextPayment = txns.FirstOrDefault(t => t.Amount < 0);
        var nextIncome = txns.FirstOrDefault(t => t.Amount > 0);
        var lowest = txns.MinBy(t => t.Balance)!;
        var highest = txns.MaxBy(t => t.Balance)!;

        if (nextPayment is not null) output.Add(new TransactionVM() { Description = "Next Payment", Amount = nextPayment.Amount, Date = nextPayment.Date });
        if (nextIncome is not null) output.Add(new TransactionVM() { Description = "Next Income", Amount = nextIncome.Amount, Date = nextIncome.Date });
        output.Add(new TransactionVM() { Description = "Lowest in 6 Months", Amount = lowest.Balance, Date = lowest.Date });
        output.Add(new TransactionVM() { Description = "Highest in 6 Months", Amount = highest.Balance, Date = highest.Date });

        return output;
    }

    private static void UpdateTxnBalance(List<TransactionVM> txns)
    {
        var currBalanceTxn = txns.First(t => t.Description.Equals("Current Balance"));
        var balance = currBalanceTxn.Balance;
        var index = Array.FindIndex(txns.ToArray(), t => t == currBalanceTxn);

        foreach (var item in txns.Where(t => t.Date > DateTime.Today.ToDateOnly()))
        {
            balance += item.Amount;
            item.Balance = balance;
        }

        if (index >= 1)
            txns[index - 1].Balance = txns[index].Balance;

        if (index <= 1)
            return;

        for (int i = index - 2; i >= 0; i--)
        {
            txns[i].Balance = txns[i + 1].Balance - txns[i+1].Amount;
        }
    }
    private static IEnumerable<TransactionVM> GetTxns(RecurringTransaction txn)
    {
        var output = new List<TransactionVM>();

        if (txn.StartDate > DateTime.Today.ToDateOnly().AddMonths(6))
            return output;

        var startDate = txn.StartDate;
        var endDate = txn.EndDate.GetValueOrDefault(DateTime.Today.ToDateOnly()).AddMonths(6);

        var dayDiff = endDate.DayNumber - startDate.DayNumber;
        var monthDiff = GetMonthDifference(startDate.ToDateTime(TimeOnly.MinValue), endDate.ToDateTime(TimeOnly.MinValue));


        switch (txn.TransactionType)
        {
            case TransactionType.Once:
                if (txn.StartDate >= DateOnly.FromDateTime(DateTime.Today) && 
                    txn.StartDate <= DateOnly.FromDateTime(DateTime.Today).AddMonths(6))
                        output.Add(new TransactionVM() { Amount = txn.Amount, Date = txn.StartDate, Description = txn.Description });
                break;
            case TransactionType.Daily:
                output.AddRange(Enumerable.Range(0, dayDiff).Select(d => new TransactionVM() { Amount = txn.Amount, Date = startDate.AddDays(d), Description = txn.Description }));
                break;
            case TransactionType.Weekly:
                output.AddRange(Enumerable.Range(0, (dayDiff / 7) + 1).Select(d => new TransactionVM() { Amount = txn.Amount, Date = startDate.AddDays(d * 7), Description = txn.Description }));
                break;
            case TransactionType.BiWeekly:
                output.AddRange(Enumerable.Range(0, (dayDiff / 14) + 1).Select(d => new TransactionVM() { Amount = txn.Amount, Date = startDate.AddDays(d * 14), Description = txn.Description }));
                break;
            case TransactionType.Monthly:
                output.AddRange(Enumerable.Range(0, monthDiff).Select(d => new TransactionVM() { Amount = txn.Amount, Date = startDate.AddMonths(d), Description = txn.Description }));
                break;
            case TransactionType.Quarterly:
                break;
            case TransactionType.Annually:
                break;
            default:
                break;
        }

        return output.Where(o => o.Date >= DateTime.Today.ToDateOnly());

    }

    private static int GetMonthDifference(DateTime startDate, DateTime endDate)
    {
        int monthsApart = 12 * (startDate.Year - endDate.Year) + startDate.Month - endDate.Month;
        return Math.Abs(monthsApart);
    }
}
