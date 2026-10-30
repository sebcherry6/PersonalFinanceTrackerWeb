namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// Direction of a transaction. Amounts are always stored as positive numbers;
/// this enum decides whether the amount adds to or subtracts from the balance.
/// </summary>
public enum TransactionType
{
    Expense,
    Income
}
