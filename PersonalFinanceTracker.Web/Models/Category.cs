namespace PersonalFinanceTracker.Web.Models;

/// <summary>
/// Transaction categories. These are stored in the database as text (see AppDbContext),
/// so you can safely reorder or add values here without corrupting existing rows.
/// Renaming a value, however, needs a data migration.
/// </summary>
public enum Category
{
    // Money in
    Salary,
    Bonus,
    Investment,
    Gift,
    Refund,

    // Money out
    Food,
    Rent,
    Bills,
    Transport,
    Entertainment,
    Shopping,
    Health,
    Travel,

    // Allowed for both directions
    Other
}

/// <summary>
/// Which categories belong to which transaction type. Used by validation (so an
/// "Income" can't be filed under "Rent") and by the views to build the dropdowns.
/// </summary>
public static class Categories
{
    public static readonly IReadOnlyList<Category> Income =
        [Category.Salary, Category.Bonus, Category.Investment, Category.Gift, Category.Refund, Category.Other];

    public static readonly IReadOnlyList<Category> Expense =
        [Category.Food, Category.Rent, Category.Bills, Category.Transport, Category.Entertainment,
         Category.Shopping, Category.Health, Category.Travel, Category.Other];

    /// <summary>
    /// True when the category exists and is allowed for the given type.
    /// Enum.IsDefined matters: model binding happily accepts "Category=999" otherwise.
    /// </summary>
    public static bool IsValidFor(Category category, TransactionType type) =>
        Enum.IsDefined(category)
        && Enum.IsDefined(type)
        && (type == TransactionType.Income ? Income : Expense).Contains(category);
}
