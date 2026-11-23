using System.Text;

namespace StatementSorter.Core;

public sealed record RepeatCharge(string Merchant, decimal Amount, bool Exact);

public static class RepeatCharges
{
    public static IReadOnlyList<RepeatCharge> SameAmount(SortResult later, SortResult earlier)
    {
        var now = SpendByMerchant(later);
        var then = SpendByMerchant(earlier);
        var matches = new List<RepeatCharge>();

        foreach (var (key, laterSpend) in now)
        {
            if (!then.TryGetValue(key, out var earlierSpend))
                continue;

            var delta = decimal.Round(Math.Abs(laterSpend.Amount - earlierSpend.Amount), 2, MidpointRounding.AwayFromZero);
            if (delta >= 1m)
                continue;

            matches.Add(new RepeatCharge(laterSpend.Display, laterSpend.Amount, delta == 0m));
        }

        return matches
            .OrderByDescending(charge => charge.Amount)
            .ThenBy(charge => charge.Merchant, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, MerchantSpend> SpendByMerchant(SortResult result)
    {
        var groups = new Dictionary<string, MerchantSpend>(StringComparer.Ordinal);
        foreach (var transaction in result.Transactions)
        {
            if (transaction.Amount >= 0 || !CategoryCatalog.CountsTowardSpent(transaction.Category))
                continue;

            var key = MerchantKey(transaction.Description);
            if (key.Length == 0)
                continue;

            var amount = -transaction.Amount;
            if (groups.TryGetValue(key, out var existing))
                existing.Amount += amount;
            else
                groups[key] = new MerchantSpend(MerchantDisplay(transaction.Description), amount);
        }

        foreach (var spend in groups.Values)
            spend.Amount = decimal.Round(spend.Amount, 2, MidpointRounding.AwayFromZero);

        return groups;
    }

    private static string MerchantKey(string description)
    {
        var text = description.ToLowerInvariant();
        var cut = text.IndexOfAny(['*', '#']);
        if (cut > 0)
            text = text[..cut];

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                pendingSpace = false;
                builder.Append(ch);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static string MerchantDisplay(string description)
    {
        var cut = description.IndexOfAny(['*', '#']);
        var text = cut > 0 ? description[..cut] : description;
        return text.Trim().TrimEnd(' ', ',', '.', ';');
    }

    private sealed class MerchantSpend(string display, decimal amount)
    {
        public string Display { get; } = display;
        public decimal Amount { get; set; } = amount;
    }
}
