using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// What a user is allowed to send when creating or editing a transaction - shared by the
/// web form and the JSON API so both get identical validation.
///
/// Notice what is NOT here: Id and UserId. Binding straight to the Transaction entity (as the
/// original code did) let a crafted form post set those fields ("over-posting"). The server
/// fills them in instead.
/// </summary>
public sealed class TransactionInput : IValidatableObject
{
    [Required(ErrorMessage = "Add a short description.")]
    [StringLength(200, ErrorMessage = "Keep the description under 200 characters.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Nullable so an empty form field shows "Enter an amount" rather than a pre-filled 0.</summary>
    [Required(ErrorMessage = "Enter an amount.")]
    [Range(typeof(decimal), "0.01", "10000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Enter an amount between 0.01 and 10,000,000.")]
    public decimal? Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public Category Category { get; set; } = Category.Food;

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Rules that involve more than one field.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult("Choose money in or money out.", [nameof(Type)]);
        }
        else if (!Categories.IsValidFor(Category, Type))
        {
            yield return new ValidationResult(
                Type == TransactionType.Income
                    ? "That category is for spending. Pick an income category."
                    : "That category is for income. Pick a spending category.",
                [nameof(Category)]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (Date < new DateOnly(2000, 1, 1) || Date > today.AddYears(1))
        {
            yield return new ValidationResult("Choose a date between 2000 and one year from today.", [nameof(Date)]);
        }
    }

    public static TransactionInput From(Transaction t) => new()
    {
        Description = t.Description,
        Amount = t.Amount,
        Type = t.Type,
        Category = t.Category,
        Date = t.Date
    };
}
