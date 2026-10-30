using System.Globalization;
using System.Text;
using PersonalFinanceTracker.Web.Models;

namespace PersonalFinanceTracker.Web.Services;

/// <summary>
/// Builds the "Download my transactions" CSV file.
///
/// Guards against CSV/formula injection: if a description starts with = + - @ (or a tab/CR),
/// Excel and Sheets treat it as a formula, so a description like =HYPERLINK(...) could run
/// when the file is opened. Those cells get a leading apostrophe so they stay plain text.
/// </summary>
public static class CsvExporter
{
    public static byte[] Write(IEnumerable<Transaction> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Description,Category,Type,Amount");

        foreach (var t in transactions)
        {
            sb.Append(t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
              .Append(Cell(t.Description)).Append(',')
              .Append(t.Category).Append(',')
              .Append(t.Type).Append(',')
              .Append(t.Amount.ToString("0.00", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        // UTF-8 with BOM so Excel opens "£" and accented characters correctly.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        var needsQuotes = value.IndexOfAny([',', '"', '\n', '\r']) >= 0 || value.StartsWith('\'');
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
