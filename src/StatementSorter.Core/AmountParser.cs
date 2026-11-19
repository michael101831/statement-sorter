using System.Globalization;

namespace StatementSorter.Core;

public readonly record struct ParsedAmount(decimal Value, bool ExplicitSign);

public static class AmountParser
{
    public static bool TryParse(string? raw, out ParsedAmount amount)
    {
        amount = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim().Replace('\u2212', '-').Replace('–', '-');
        if (text is "-" or "—" or ".")
            return false;

        var negative = false;
        var explicitSign = false;

        if (text.StartsWith('(') && text.EndsWith(')') && text.Length > 2)
        {
            negative = true;
            explicitSign = true;
            text = text[1..^1].Trim();
        }

        if (text.EndsWith("CR", StringComparison.OrdinalIgnoreCase))
        {
            explicitSign = true;
            text = text[..^2].Trim();
        }
        else if (text.EndsWith("DR", StringComparison.OrdinalIgnoreCase))
        {
            negative = true;
            explicitSign = true;
            text = text[..^2].Trim();
        }

        if (text.EndsWith('-'))
        {
            negative = true;
            explicitSign = true;
            text = text[..^1].Trim();
        }

        text = text.Replace("$", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        if (text.StartsWith('+'))
        {
            explicitSign = true;
            text = text[1..];
        }
        else if (text.StartsWith('-'))
        {
            negative = true;
            explicitSign = true;
            text = text[1..];
        }

        if (text.Length == 0)
            return false;

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return false;

        value = negative ? -Math.Abs(value) : value;
        amount = new ParsedAmount(value, explicitSign);
        return true;
    }
}
