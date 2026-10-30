namespace PersonalFinanceTracker.Web.Models;

/// <summary>Per-user display preferences. At most one row per user (unique index on UserId).</summary>
public class UserSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    /// <summary>One of <see cref="PreferenceOptions.Themes"/>.</summary>
    public string Theme { get; set; } = PreferenceOptions.DefaultTheme;

    /// <summary>One of <see cref="PreferenceOptions.Currencies"/> (ISO 4217 code).</summary>
    public string Currency { get; set; } = PreferenceOptions.DefaultCurrency;
}

/// <summary>
/// The only values the settings form accepts. Validating against an allow-list means
/// nothing unexpected can be saved and later echoed into the page.
/// </summary>
public static class PreferenceOptions
{
    public const string DefaultTheme = "system";
    public const string DefaultCurrency = "GBP";

    public static readonly IReadOnlyList<string> Themes = ["system", "light", "dark"];

    public static readonly IReadOnlyDictionary<string, string> Currencies = new Dictionary<string, string>
    {
        ["GBP"] = "£",
        ["EUR"] = "€",
        ["USD"] = "$"
    };
}
