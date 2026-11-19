using System.Globalization;

namespace StatementSorter.Core;

public static class DateParser
{
    private static readonly string[] Formats =
    [
        "M/d/yyyy",
        "M/d/yy",
        "yyyy-M-d",
        "yyyy/M/d",
        "d-MMM-yyyy",
        "MMM d, yyyy",
        "MMM d yyyy",
        "MMMM d, yyyy",
        "MMMM d yyyy",
        "M-d-yyyy",
        "M.d.yyyy",
    ];

    public static bool TryParse(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = StripTime(raw.Trim().Trim('"'));
        var culture = CultureInfo.GetCultureInfo("en-US");

        if (DateOnly.TryParseExact(text, Formats, culture, DateTimeStyles.AllowWhiteSpaces, out date) &&
            IsPlausible(date))
        {
            return true;
        }

        if (TryDayFirst(text, out date) && IsPlausible(date))
            return true;

        date = default;
        return false;
    }

    private static string StripTime(string text)
    {
        var separator = text.IndexOf('T');
        if (separator > 4 && text.AsSpan(separator + 1).Contains(':'))
            return text[..separator];

        var value = text;
        if (value.EndsWith(" AM", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(" PM", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^3].TrimEnd();
        }

        var space = value.LastIndexOf(' ');
        if (space > 0 && value.AsSpan(space + 1).Contains(':'))
            return value[..space].Trim();

        return value;
    }

    // US exports are month/day/year. When the first number cannot be a month, read day/month/year.
    private static bool TryDayFirst(string text, out DateOnly date)
    {
        date = default;
        var parts = text.Split('/', '-');
        if (parts.Length != 3)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month))
            return false;
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            return false;
        if (day <= 12)
            return false;
        if (year < 100)
            year += 2000;

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsPlausible(DateOnly date) => date.Year is >= 1990 and <= 2100;
}
