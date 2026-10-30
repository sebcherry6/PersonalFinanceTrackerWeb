using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Web.Data;
using PersonalFinanceTracker.Web.Models;

namespace PersonalFinanceTracker.Web.Services;

/// <summary>
/// The signed-in user's theme and currency for the current request, plus money formatting.
///
/// Views use it through `@inject UserPreferences Prefs` (see _ViewImports.cshtml), e.g.
/// `@Prefs.Format(amount)`. It's loaded once per request by LoadPreferencesFilter, before
/// any view renders.
///
/// Money is formatted by hand rather than with ToString("C") on purpose: "C" depends on the
/// server's culture, so the same app showed "£" on a UK laptop and "¤" in a Linux container.
/// </summary>
public sealed class UserPreferences(AppDbContext db)
{
    public string Theme { get; private set; } = PreferenceOptions.DefaultTheme;
    public string Currency { get; private set; } = PreferenceOptions.DefaultCurrency;

    public string Symbol => PreferenceOptions.Currencies.GetValueOrDefault(Currency, "£");

    public async Task LoadAsync(string userId, CancellationToken ct = default)
    {
        var settings = await db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings is null) return;

        // Re-check against the allow-lists in case a value was edited directly in the database.
        if (PreferenceOptions.Themes.Contains(settings.Theme)) Theme = settings.Theme;
        if (PreferenceOptions.Currencies.ContainsKey(settings.Currency)) Currency = settings.Currency;
    }

    /// <summary>"£1,234.50" or "−£12.00".</summary>
    public string Format(decimal amount)
    {
        var digits = Math.Abs(amount).ToString("#,##0.00", CultureInfo.InvariantCulture);
        return amount < 0 ? $"\u2212{Symbol}{digits}" : $"{Symbol}{digits}";
    }

    /// <summary>"+£50.00" for money in, "−£12.00" for money out.</summary>
    public string FormatSigned(decimal amount, TransactionType type) =>
        (type == TransactionType.Income ? "+" : "\u2212") + Format(Math.Abs(amount));
}
