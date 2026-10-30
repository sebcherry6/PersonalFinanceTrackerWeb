namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// A monthly spending limit for one expense category. Each user can have at most one
/// budget per category (enforced by a unique index); "spent" is calculated live from
/// this calendar month's expense transactions, so it never goes stale.
/// </summary>
public class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Category Category { get; set; }

    public decimal MonthlyLimit { get; set; }
}
