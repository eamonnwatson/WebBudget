using Microsoft.EntityFrameworkCore;
using WebBudget.Data.Model;

namespace WebBudget.Data;

public class BudgetContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<RecurringTransaction> RecurringTransactions { get; set; }

    public BudgetContext(DbContextOptions options) : base(options)
    {
        Database.EnsureCreated();
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().HasData(new Account() { Id = 1, LastUpdated = DateTime.Now, CurrentBalance = 0M });
    }
}
