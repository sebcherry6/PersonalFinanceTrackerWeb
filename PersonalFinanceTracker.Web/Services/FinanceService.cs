using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Web.Data;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Models.ViewModels;

namespace PersonalFinanceTracker.Web.Services;

/// <summary>
/// All reading and writing of financial data goes through this class.
///
/// THE SECURITY RULE: every method takes the caller's userId and every query filters on it.
/// A request for someone else's transaction ID simply finds nothing (and the controller
/// returns 404), so IDs can't be guessed or swapped to reach another person's data.
/// Controllers never pass a UserId that came from the request body - only the one from
/// the authenticated cookie or token.
///
/// Registered as "scoped" in Program.cs: one instance per HTTP request, sharing that
/// request's AppDbContext. (The original controller did `new FinanceService(context)`,
/// bypassing dependency injection.)
/// </summary>
public sealed class FinanceService(AppDbContext db, TimeProvider clock)
{
    public const int PageSize = 25;

    /// <summary>Today's date in UTC. TimeProvider makes this replaceable in tests.</summary>
    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>The one place transaction reads start from - already filtered to the owner.</summary>
    private IQueryable<Transaction> TransactionsOf(string userId) =>
        db.Transactions.AsNoTracking().Where(t => t.UserId == userId);

    // =====================================================================
    // Transactions
    // =====================================================================

    public async Task<PagedResult<Transaction>> SearchTransactionsAsync(
        string userId, string? search, TransactionType? type, int page, CancellationToken ct = default)
    {
        var query = TransactionsOf(userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // EF sends the pattern as a SQL parameter (no injection possible). We also escape
            // LIKE wildcards so searching for "50%" means the literal text "50%".
            var term = search.Trim();
            if (term.Length > 100) term = term[..100];
            var pattern = "%" + EscapeLike(term) + "%";
            query = query.Where(t => EF.Functions.Like(t.Description, pattern, "\\"));
        }

        if (type is { } wanted)
        {
            query = query.Where(t => t.Type == wanted);
        }

        var total = await query.CountAsync(ct);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, lastPage);

        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return new PagedResult<Transaction>(items, page, PageSize, total);
    }

    public Task<List<Transaction>> GetAllTransactionsAsync(string userId, CancellationToken ct = default) =>
        TransactionsOf(userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<Transaction?> GetTransactionAsync(Guid id, string userId, CancellationToken ct = default) =>
        TransactionsOf(userId).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Transaction> AddTransactionAsync(string userId, TransactionInput input, CancellationToken ct = default)
    {
        var transaction = new Transaction
        {
            UserId = userId,                       // from the session, never from input
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        Apply(input, transaction);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return transaction;
    }

    /// <summary>Returns false when the transaction doesn't exist or belongs to someone else.</summary>
    public async Task<bool> UpdateTransactionAsync(Guid id, string userId, TransactionInput input, CancellationToken ct = default)
    {
        // Load the existing row *with the owner check*, then copy only the editable fields.
        // (The original called db.Update(postedEntity), which trusted every posted field.)
        var existing = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (existing is null) return false;

        Apply(input, existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteTransactionAsync(Guid id, string userId, CancellationToken ct = default) =>
        await db.Transactions.Where(t => t.Id == id && t.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    private static void Apply(TransactionInput input, Transaction target)
    {
        target.Description = input.Description.Trim();
        // Amount is non-null here: [Required] has already been checked by validation.
        target.Amount = decimal.Round(input.Amount ?? 0, 2, MidpointRounding.ToEven);
        target.Type = input.Type;
        target.Category = input.Category;
        target.Date = input.Date;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // =====================================================================
    // Dashboard & analytics
    // =====================================================================

    /// <summary>Just the columns needed for totals, so we don't load descriptions we won't use.</summary>
    private sealed record MoneyRow(Category Category, TransactionType Type, decimal Amount, DateOnly Date);

    private Task<List<MoneyRow>> MoneyRowsAsync(string userId, DateOnly? from, CancellationToken ct)
    {
        var query = TransactionsOf(userId);
        if (from is { } start) query = query.Where(t => t.Date >= start);
        return query.Select(t => new MoneyRow(t.Category, t.Type, t.Amount, t.Date)).ToListAsync(ct);
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string userId, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(Today.Year, Today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        // Totals are summed in memory: SQLite stores decimals as text and can't SUM them
        // exactly. Rows are already narrowed to this one user, so the set stays small.
        var rows = await MoneyRowsAsync(userId, from: null, ct);
        var thisMonth = rows.Where(r => r.Date >= monthStart && r.Date < nextMonth).ToList();

        var recent = await TransactionsOf(userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Take(6)
            .ToListAsync(ct);

        return new DashboardViewModel
        {
            Balance = SumOf(rows, TransactionType.Income) - SumOf(rows, TransactionType.Expense),
            Month = monthStart,
            MonthIn = SumOf(thisMonth, TransactionType.Income),
            MonthOut = SumOf(thisMonth, TransactionType.Expense),
            MonthSpending = SpendingByCategory(thisMonth),
            Recent = recent,
            HasAnyTransactions = rows.Count > 0
        };
    }

    public async Task<AnalyticsViewModel> GetAnalyticsAsync(string userId, int months = 6, CancellationToken ct = default)
    {
        months = Math.Clamp(months, 1, 24);
        var currentMonth = new DateOnly(Today.Year, Today.Month, 1);
        var from = currentMonth.AddMonths(-(months - 1));
        var to = currentMonth.AddMonths(1).AddDays(-1);

        var inPeriod = await TransactionsOf(userId)
            .Where(t => t.Date >= from && t.Date <= to)
            .ToListAsync(ct);

        var monthly = Enumerable.Range(0, months)
            .Select(i => from.AddMonths(i))
            .Select(m =>
            {
                var end = m.AddMonths(1);
                var slice = inPeriod.Where(t => t.Date >= m && t.Date < end).ToList();
                return new MonthTotal(
                    m,
                    slice.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    slice.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount));
            })
            .ToList();

        var expenses = inPeriod.Where(t => t.Type == TransactionType.Expense).ToList();

        return new AnalyticsViewModel
        {
            From = from,
            To = to,
            Months = monthly,
            TotalIn = monthly.Sum(m => m.In),
            TotalOut = monthly.Sum(m => m.Out),
            Spending = expenses
                .GroupBy(t => t.Category)
                .Select(g => new CategoryTotal(g.Key, g.Sum(t => t.Amount)))
                .OrderByDescending(c => c.Total)
                .ToList(),
            LargestExpense = expenses.OrderByDescending(t => t.Amount).FirstOrDefault()
        };
    }

    private static decimal SumOf(IEnumerable<MoneyRow> rows, TransactionType type) =>
        rows.Where(r => r.Type == type).Sum(r => r.Amount);

    private static List<CategoryTotal> SpendingByCategory(IEnumerable<MoneyRow> rows) =>
        rows.Where(r => r.Type == TransactionType.Expense)
            .GroupBy(r => r.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(r => r.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();

    // =====================================================================
    // Budgets
    // =====================================================================

    public async Task<IReadOnlyList<BudgetProgress>> GetBudgetProgressAsync(string userId, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(Today.Year, Today.Month, 1);

        var budgets = await db.Budgets.AsNoTracking().Where(b => b.UserId == userId).ToListAsync(ct);
        if (budgets.Count == 0) return [];

        var spentByCategory = (await MoneyRowsAsync(userId, monthStart, ct))
            .Where(r => r.Type == TransactionType.Expense && r.Date < monthStart.AddMonths(1))
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        return budgets
            .Select(b => new BudgetProgress(b.Id, b.Category, b.MonthlyLimit, spentByCategory.GetValueOrDefault(b.Category)))
            .OrderByDescending(b => b.Limit == 0 ? 0 : b.Spent / b.Limit)
            .ToList();
    }

    /// <summary>Creates the budget, or updates the limit if this category already has one.</summary>
    public async Task SaveBudgetAsync(string userId, BudgetInput input, CancellationToken ct = default)
    {
        var existing = await db.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.Category == input.Category, ct);
        var limit = decimal.Round(input.MonthlyLimit ?? 0, 2);

        if (existing is null)
        {
            db.Budgets.Add(new Budget { UserId = userId, Category = input.Category, MonthlyLimit = limit });
        }
        else
        {
            existing.MonthlyLimit = limit;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteBudgetAsync(Guid id, string userId, CancellationToken ct = default) =>
        await db.Budgets.Where(b => b.Id == id && b.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    // =====================================================================
    // Savings goals
    // =====================================================================

    public Task<List<SavingsGoal>> GetGoalsAsync(string userId, CancellationToken ct = default) =>
        db.SavingsGoals.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddGoalAsync(string userId, GoalInput input, CancellationToken ct = default)
    {
        db.SavingsGoals.Add(new SavingsGoal
        {
            UserId = userId,
            Name = input.Name.Trim(),
            TargetAmount = decimal.Round(input.TargetAmount ?? 0, 2),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Adds to (or withdraws from) a goal. Saved amount never drops below zero.</summary>
    public async Task<bool> ContributeToGoalAsync(Guid id, string userId, GoalContributionInput input, CancellationToken ct = default)
    {
        var goal = await db.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, ct);
        if (goal is null) return false;

        var change = decimal.Round(input.Amount ?? 0, 2) * (input.Withdraw ? -1 : 1);
        goal.SavedAmount = Math.Max(0, goal.SavedAmount + change);

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteGoalAsync(Guid id, string userId, CancellationToken ct = default) =>
        await db.SavingsGoals.Where(g => g.Id == id && g.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    // =====================================================================
    // Settings
    // =====================================================================

    /// <summary>The user's settings, or the defaults if they've never saved any.</summary>
    public async Task<UserSettings> GetSettingsAsync(string userId, CancellationToken ct = default) =>
        await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct)
        ?? new UserSettings { UserId = userId };

    public async Task SaveSettingsAsync(string userId, SettingsInput input, CancellationToken ct = default)
    {
        // Looked up by the *signed-in* user. The original form posted a hidden UserId and the
        // service saved to whichever user it named - so anyone could overwrite anyone's settings.
        var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            db.UserSettings.Add(settings);
        }

        settings.Theme = input.Theme;
        settings.Currency = input.Currency;
        await db.SaveChangesAsync(ct);
    }
}
