namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// Something the user is saving towards. SavedAmount is moved by the
/// "Add" and "Withdraw" buttons on the Goals page and can never go below zero.
/// </summary>
public class SavingsGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }

    public decimal SavedAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public bool IsComplete => SavedAmount >= TargetAmount;
}
