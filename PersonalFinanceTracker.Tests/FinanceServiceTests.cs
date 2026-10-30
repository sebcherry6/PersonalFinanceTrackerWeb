using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Web.Data;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Tests;

/// <summary>Business logic tests against a fresh in-memory SQLite database per test.</summary>
public sealed class FinanceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly FinanceService _finance;
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private const string Alice = "alice";
    private const string Bob = "bob";

    public FinanceServiceTests()
    {
        _connection.Open();   // an in-memory database lives as long as its connection
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        // Finance rows have a foreign key to AspNetUsers, so the users must exist.
        _db.Users.AddRange(new IdentityUser { Id = Alice, UserName = Alice }, new IdentityUser { Id = Bob, UserName = Bob });
        _db.SaveChanges();

        _finance = new FinanceService(_db, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<Transaction> Add(string user, TransactionType type, decimal amount, Category category, DateOnly date, string description = "x") =>
        _finance.AddTransactionAsync(user, new TransactionInput
        {
            Type = type, Amount = amount, Category = category, Date = date, Description = description
        });

    [Fact]
    public async Task Dashboard_totals_cover_this_month_and_all_time_balance()
    {
        await Add(Alice, TransactionType.Income, 2000m, Category.Salary, new DateOnly(2026, 10, 1));
        await Add(Alice, TransactionType.Expense, 650.50m, Category.Rent, new DateOnly(2026, 10, 2));
        await Add(Alice, TransactionType.Expense, 49.50m, Category.Food, new DateOnly(2026, 10, 3));
        await Add(Alice, TransactionType.Expense, 100m, Category.Food, new DateOnly(2026, 9, 20));   // last month
        await Add(Bob, TransactionType.Income, 99999m, Category.Salary, new DateOnly(2026, 10, 1));   // someone else

        var d = await _finance.GetDashboardAsync(Alice);

        Assert.Equal(2000m, d.MonthIn);
        Assert.Equal(700m, d.MonthOut);
        Assert.Equal(1200m, d.Balance);              // 2000 - 650.50 - 49.50 - 100
        Assert.Equal(Category.Rent, d.MonthSpending[0].Category);
        Assert.Equal(4, d.Recent.Count);             // Bob's transaction is not visible
    }

    [Fact]
    public async Task Update_and_delete_only_work_for_the_owner()
    {
        var t = await Add(Alice, TransactionType.Expense, 10m, Category.Food, new DateOnly(2026, 10, 1), "lunch");
        var edit = new TransactionInput { Type = TransactionType.Expense, Amount = 1m, Category = Category.Food, Date = t.Date, Description = "changed" };

        Assert.False(await _finance.UpdateTransactionAsync(t.Id, Bob, edit));
        Assert.False(await _finance.DeleteTransactionAsync(t.Id, Bob));
        Assert.Null(await _finance.GetTransactionAsync(t.Id, Bob));

        var unchanged = await _finance.GetTransactionAsync(t.Id, Alice);
        Assert.Equal("lunch", unchanged!.Description);

        Assert.True(await _finance.UpdateTransactionAsync(t.Id, Alice, edit));
        Assert.True(await _finance.DeleteTransactionAsync(t.Id, Alice));
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        await Add(Alice, TransactionType.Expense, 5m, Category.Shopping, new DateOnly(2026, 10, 1), "50% off sale");
        await Add(Alice, TransactionType.Expense, 5m, Category.Shopping, new DateOnly(2026, 10, 1), "500 widgets");

        var results = await _finance.SearchTransactionsAsync(Alice, "50%", type: null, page: 1);

        Assert.Single(results.Items);
        Assert.Equal("50% off sale", results.Items[0].Description);
    }

    [Fact]
    public async Task Budget_progress_counts_only_this_months_spending_in_that_category()
    {
        await _finance.SaveBudgetAsync(Alice, new BudgetInput { Category = Category.Food, MonthlyLimit = 100m });
        await Add(Alice, TransactionType.Expense, 80m, Category.Food, new DateOnly(2026, 10, 5));
        await Add(Alice, TransactionType.Expense, 40m, Category.Food, new DateOnly(2026, 10, 9));
        await Add(Alice, TransactionType.Expense, 500m, Category.Food, new DateOnly(2026, 9, 30));    // last month
        await Add(Alice, TransactionType.Expense, 500m, Category.Travel, new DateOnly(2026, 10, 9));  // other category

        var budget = Assert.Single(await _finance.GetBudgetProgressAsync(Alice));

        Assert.Equal(120m, budget.Spent);
        Assert.True(budget.IsOver);
        Assert.Equal(-20m, budget.Remaining);
    }

    [Fact]
    public async Task Saving_a_budget_twice_updates_the_limit()
    {
        await _finance.SaveBudgetAsync(Alice, new BudgetInput { Category = Category.Bills, MonthlyLimit = 100m });
        await _finance.SaveBudgetAsync(Alice, new BudgetInput { Category = Category.Bills, MonthlyLimit = 250m });

        var budget = Assert.Single(await _finance.GetBudgetProgressAsync(Alice));
        Assert.Equal(250m, budget.Limit);
    }

    [Fact]
    public async Task Goal_withdrawals_never_go_below_zero()
    {
        await _finance.AddGoalAsync(Alice, new GoalInput { Name = "Holiday", TargetAmount = 500m });
        var goal = Assert.Single(await _finance.GetGoalsAsync(Alice));

        await _finance.ContributeToGoalAsync(goal.Id, Alice, new GoalContributionInput { Amount = 60m });
        await _finance.ContributeToGoalAsync(goal.Id, Alice, new GoalContributionInput { Amount = 100m, Withdraw = true });
        Assert.False(await _finance.ContributeToGoalAsync(goal.Id, Bob, new GoalContributionInput { Amount = 1000m }));

        Assert.Equal(0m, Assert.Single(await _finance.GetGoalsAsync(Alice)).SavedAmount);
    }

    [Fact]
    public async Task Settings_are_saved_for_the_signed_in_user_only()
    {
        await _finance.SaveSettingsAsync(Alice, new SettingsInput { Theme = "dark", Currency = "EUR" });

        Assert.Equal("EUR", (await _finance.GetSettingsAsync(Alice)).Currency);
        Assert.Equal(PreferenceOptions.DefaultCurrency, (await _finance.GetSettingsAsync(Bob)).Currency);
    }

    [Fact]
    public async Task Deleting_a_user_deletes_their_data()
    {
        await Add(Alice, TransactionType.Expense, 10m, Category.Food, new DateOnly(2026, 10, 1));
        await _finance.SaveBudgetAsync(Alice, new BudgetInput { Category = Category.Food, MonthlyLimit = 50m });

        _db.Users.Remove(await _db.Users.SingleAsync(u => u.Id == Alice));
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.Transactions.Where(t => t.UserId == Alice).ToListAsync());
        Assert.Empty(await _db.Budgets.Where(b => b.UserId == Alice).ToListAsync());
    }

    [Theory]
    [InlineData(TransactionType.Income, Category.Salary, true)]
    [InlineData(TransactionType.Income, Category.Rent, false)]
    [InlineData(TransactionType.Expense, Category.Food, true)]
    [InlineData(TransactionType.Expense, Category.Bonus, false)]
    [InlineData(TransactionType.Expense, Category.Other, true)]
    [InlineData(TransactionType.Income, (Category)999, false)]
    public void Category_rules(TransactionType type, Category category, bool valid) =>
        Assert.Equal(valid, Categories.IsValidFor(category, type));

    [Fact]
    public void Csv_export_neutralises_formulas()
    {
        var csv = System.Text.Encoding.UTF8.GetString(CsvExporter.Write(
        [
            new Transaction { Description = "=HYPERLINK(\"http://evil\")", Amount = 1m, Date = new DateOnly(2026, 1, 1) }
        ]));

        Assert.DoesNotContain(",=HYPERLINK", csv);
        Assert.Contains("'=HYPERLINK", csv);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
