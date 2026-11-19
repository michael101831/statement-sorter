namespace StatementSorter.Core;

public static class CategoryCatalog
{
    public const string Uncategorized = "Uncategorized";

    // Earlier rules win. "uber eats" has to stay ahead of "uber".
    private static readonly (string Category, string[] Keywords)[] Rules =
    [
        ("Income", ["payroll", "salary", "paycheck", "pay check", "direct dep", "direct deposit"]),
        ("Transfers", ["venmo", "zelle", "transfer", "payment thank you", "online payment", "ach pmt"]),
        ("Fees", ["overdraft", "late fee", "service fee", "interest charge", "foreign transaction"]),
        ("Groceries", ["kroger", "aldi", "lidl", "trader joe", "whole foods", "safeway", "publix", "wegmans", "costco", "grocery", "supermarket", "food lion", "meijer"]),
        ("Dining", ["uber eats", "doordash", "grubhub", "starbucks", "mcdonald", "chipotle", "dunkin", "taco bell", "wendy", "restaurant", "cafe", "coffee"]),
        ("Transport", ["uber", "lyft", "shell", "chevron", "exxon", "mobil", "parking", "metro", "transit", "toll", "bp"]),
        ("Utilities", ["comcast", "xfinity", "verizon", "t-mobile", "at&t", "electric", "utility", "internet"]),
        ("Health", ["cvs", "walgreens", "pharmacy", "rite aid", "hospital", "dental", "clinic"]),
        ("Entertainment", ["netflix", "spotify", "hulu", "disney+", "steam", "youtube", "cinema", "amc"]),
        ("Shopping", ["amazon", "amzn", "ebay", "etsy", "walmart", "target", "best buy"]),
        ("Housing", ["rent", "mortgage", "landlord", "apartment"]),
    ];

    public static (string Category, string Source, string? Keyword) Match(string description, string? categoryColumn)
    {
        if (!string.IsNullOrWhiteSpace(categoryColumn))
        {
            var cleaned = CleanLabel(categoryColumn);
            if (cleaned.Length > 0 &&
                !cleaned.Equals(Uncategorized, StringComparison.OrdinalIgnoreCase) &&
                !cleaned.Equals("N/A", StringComparison.OrdinalIgnoreCase) &&
                cleaned != "-")
            {
                return (cleaned, "column", null);
            }
        }

        var haystack = description.ToLowerInvariant();
        foreach (var (category, keywords) in Rules)
        {
            foreach (var keyword in keywords)
            {
                if (ContainsWord(haystack, keyword))
                    return (category, "rule", keyword);
            }
        }

        return (Uncategorized, "uncategorized", null);
    }

    public static bool CountsTowardSpent(string category)
    {
        var name = category.Trim().ToLowerInvariant();
        return name is not ("income" or "transfer" or "transfers" or "payment" or "payments");
    }

    public static bool CountsTowardReceived(string category)
    {
        var name = category.Trim().ToLowerInvariant();
        return name is not ("transfer" or "transfers" or "payment" or "payments");
    }

    private static string CleanLabel(string value)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var word = parts[i].ToLowerInvariant();
            if (word.Length == 0)
                continue;
            parts[i] = word.Length == 1
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..];
        }

        return string.Join(' ', parts);
    }

    private static bool ContainsWord(string haystack, string keyword)
    {
        var index = 0;
        while ((index = haystack.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !char.IsLetter(haystack[index - 1]);
            var end = index + keyword.Length;
            var after = end >= haystack.Length || !char.IsLetter(haystack[end]);
            if (before && after)
                return true;
            index = end;
        }

        return false;
    }
}
