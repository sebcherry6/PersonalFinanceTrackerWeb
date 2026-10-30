namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// One income or expense entry. Every row belongs to exactly one user (UserId),
/// and every query in FinanceService filters on it - that filter is what stops
/// one user from reading or changing another user's money.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner. Set by the server from the signed-in user, never from form input.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Always positive. <see cref="Type"/> decides the direction.</summary>
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public Category Category { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>The day the money moved. DateOnly avoids time-zone surprises around midnight.</summary>
    public DateOnly Date { get; set; }

    /// <summary>When the row was created; used to keep same-day transactions in a stable order.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
