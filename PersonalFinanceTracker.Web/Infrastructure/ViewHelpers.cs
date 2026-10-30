using System.Globalization;

namespace PersonalFinanceTracker.Web.Infrastructure;

/// <summary>Small formatting helpers for the Razor views.</summary>
public static class ViewHelpers
{
    /// <summary>
    /// A number for an SVG attribute, always with "." as the decimal separator.
    /// (On a server with a comma-decimal culture, width="42,5" would break the chart.)
    /// </summary>
    public static string Svg(decimal value) =>
        decimal.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Value as a percentage of max, clamped to 0-100.</summary>
    public static decimal Percent(decimal value, decimal max) =>
        max <= 0 ? 0 : Math.Clamp(value / max * 100, 0, 100);

    /// <summary>"October 2026"</summary>
    public static string MonthName(DateOnly month) =>
        month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"30 Oct 2026"</summary>
    public static string ShortDate(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // InvariantCulture is used for dates on purpose: its month names are English and it
    // exists on every server, including containers built without culture data.
}
