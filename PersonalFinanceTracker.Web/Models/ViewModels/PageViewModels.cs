using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Web.Models.ViewModels;

/// <summary>A page of results plus what's needed to draw "previous / next" links.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

// ---------------- Transactions ----------------

public sealed class TransactionListViewModel
{
    public required PagedResult<Transaction> Results { get; init; }
    public string? Search { get; init; }
    public TransactionType? Type { get; init; }
}

// ---------------- Dashboard & analytics ----------------

public sealed record CategoryTotal(Category Category, decimal Total);

public sealed record MonthTotal(DateOnly Month, decimal In, decimal Out)
{
    public decimal Net => In - Out;
}

public sealed class DashboardViewModel
{
    public decimal Balance { get; init; }
    public DateOnly Month { get; init; }
    public decimal MonthIn { get; init; }
    public decimal MonthOut { get; init; }
    public decimal MonthNet => MonthIn - MonthOut;
    public IReadOnlyList<CategoryTotal> MonthSpending { get; init; } = [];
    public IReadOnlyList<Transaction> Recent { get; init; } = [];
    public bool HasAnyTransactions { get; init; }
}

public sealed class AnalyticsViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public IReadOnlyList<MonthTotal> Months { get; init; } = [];
    public IReadOnlyList<CategoryTotal> Spending { get; init; } = [];
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }

    /// <summary>Share of income kept over the period, or null when there was no income.</summary>
    public decimal? SavingsRate => TotalIn > 0 ? (TotalIn - TotalOut) / TotalIn : null;

    public decimal AverageMonthlySpend => Months.Count == 0 ? 0 : TotalOut / Months.Count;
    public Transaction? LargestExpense { get; init; }
    public bool HasData => TotalIn > 0 || TotalOut > 0;
}

// ---------------- Budgets ----------------

public sealed record BudgetProgress(Guid Id, Category Category, decimal Limit, decimal Spent)
{
    public decimal Remaining => Limit - Spent;
    public bool IsOver => Spent > Limit;
}

public sealed class BudgetInput
{
    public Category Category { get; set; } = Category.Food;

    [Range(typeof(decimal), "1", "10000000", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter a monthly limit between 1 and 10,000,000.")]
    [Required(ErrorMessage = "Enter a monthly limit.")]
    [Display(Name = "Monthly limit")]
    public decimal? MonthlyLimit { get; set; }
}

public sealed class BudgetsViewModel
{
    public required DateOnly Month { get; init; }
    public required IReadOnlyList<BudgetProgress> Budgets { get; init; }
    public BudgetInput NewBudget { get; init; } = new();
}

// ---------------- Goals ----------------

public sealed class GoalInput
{
    [Required(ErrorMessage = "Give the goal a name.")]
    [StringLength(80, ErrorMessage = "Keep the name under 80 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "10000000", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter a target between 1 and 10,000,000.")]
    [Required(ErrorMessage = "Enter a target amount.")]
    [Display(Name = "Target")]
    public decimal? TargetAmount { get; set; }
}

public sealed class GoalContributionInput
{
    [Range(typeof(decimal), "0.01", "10000000", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter an amount between 0.01 and 10,000,000.")]
    [Required]
    public decimal? Amount { get; set; }

    /// <summary>True when the "Withdraw" button was pressed instead of "Add".</summary>
    public bool Withdraw { get; set; }
}

public sealed class GoalsViewModel
{
    public required IReadOnlyList<SavingsGoal> Goals { get; init; }
    public GoalInput NewGoal { get; init; } = new();
}

// ---------------- Settings ----------------

public sealed class SettingsInput : IValidatableObject
{
    public string Theme { get; set; } = PreferenceOptions.DefaultTheme;
    public string Currency { get; set; } = PreferenceOptions.DefaultCurrency;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!PreferenceOptions.Themes.Contains(Theme))
            yield return new ValidationResult("Choose a theme from the list.", [nameof(Theme)]);
        if (!PreferenceOptions.Currencies.ContainsKey(Currency))
            yield return new ValidationResult("Choose a currency from the list.", [nameof(Currency)]);
    }
}

public sealed class DeleteAccountInput
{
    [Required(ErrorMessage = "Enter your password to confirm.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    [Display(Name = "Your password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class SettingsViewModel
{
    public required SettingsInput Preferences { get; init; }
    public required string Email { get; init; }
    public DeleteAccountInput DeleteAccount { get; init; } = new();
}

// ---------------- Errors & charts ----------------

public sealed class ErrorViewModel
{
    public int StatusCode { get; init; }
    public string? RequestId { get; init; }
}

/// <summary>Input for the _Bar partial: draws value/max as a horizontal bar.</summary>
public sealed record BarModel(decimal Value, decimal Max, string Tone = "neutral");
