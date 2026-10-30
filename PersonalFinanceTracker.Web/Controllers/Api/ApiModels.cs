using System.ComponentModel.DataAnnotations;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Models.ViewModels;

namespace PersonalFinanceTracker.Web.Controllers.Api;

/// <summary>Body of POST /api/auth/token.</summary>
public sealed class TokenRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// What the API returns for a transaction. A separate shape from the database entity
/// so internal fields (UserId, CreatedAtUtc) are never exposed by accident.
/// </summary>
public sealed record TransactionDto(Guid Id, DateOnly Date, string Description, decimal Amount, TransactionType Type, Category Category)
{
    public static TransactionDto From(Transaction t) => new(t.Id, t.Date, t.Description, t.Amount, t.Type, t.Category);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record SummaryDto(decimal Balance, DateOnly Month, decimal MonthIn, decimal MonthOut, IReadOnlyList<CategoryTotal> MonthSpending);
