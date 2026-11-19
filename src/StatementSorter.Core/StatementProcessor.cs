namespace StatementSorter.Core;

public static class StatementProcessor
{
    private static readonly char[] Delimiters = [',', ';', '\t'];

    public static SortResult Process(string csvText)
    {
        ArgumentNullException.ThrowIfNull(csvText);

        if (string.IsNullOrWhiteSpace(csvText))
            throw new StatementFormatException("The file is empty.");

        if (csvText.Contains('\0'))
            throw new StatementFormatException("That file isn't a CSV. Export the statement as CSV and try again.");

        foreach (var delimiter in Delimiters)
        {
            var records = CsvReader.Read(csvText, delimiter);
            if (TryFindHeader(records, out var headerIndex, out var map))
                return Build(records, headerIndex, map!);
        }

        throw new StatementFormatException(
            "Couldn't find a header row. The file needs a date column and an amount, debit, or credit column.");
    }

    private static bool TryFindHeader(
        IReadOnlyList<CsvRecord> records,
        out int headerIndex,
        out ColumnMap? map)
    {
        var limit = Math.Min(records.Count, 40);
        for (var i = 0; i < limit; i++)
        {
            var candidate = ColumnMapper.TryMap(records[i].Fields);
            if (candidate is not null)
            {
                headerIndex = i;
                map = candidate;
                return true;
            }
        }

        headerIndex = -1;
        map = null;
        return false;
    }

    private static SortResult Build(IReadOnlyList<CsvRecord> records, int headerIndex, ColumnMap map)
    {
        var drafts = new List<Draft>();
        var issues = new List<RowIssue>();

        for (var i = headerIndex + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Fields.All(string.IsNullOrWhiteSpace))
                continue;

            if (IsRepeatedHeader(record.Fields, map))
                continue;

            var dateText = Field(record.Fields, map.TransactionDate);
            if (!DateParser.TryParse(dateText, out var date))
            {
                issues.Add(new RowIssue(
                    record.LineNumber,
                    SummaryReason(record.Fields, dateText),
                    Preview(record.Fields)));
                continue;
            }

            if (!TryReadAmount(record.Fields, map, out var parsed))
            {
                issues.Add(new RowIssue(record.LineNumber, "Missing amount.", Preview(record.Fields)));
                continue;
            }

            var description = Field(record.Fields, map.Description);
            if (description.Length == 0)
                description = "(no description)";

            drafts.Add(new Draft
            {
                Line = record.LineNumber,
                Date = date,
                Description = description,
                Amount = parsed.Value,
                Origin = parsed.Origin,
                Type = Field(record.Fields, map.Type),
                CategoryText = Field(record.Fields, map.Category),
            });
        }

        var fileUsesSignedAmounts = drafts.Exists(d =>
            (d.Origin == AmountOrigin.Explicit && d.Amount < 0) || d.Origin == AmountOrigin.DebitCredit);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var transactions = new List<Transaction>();
        var duplicates = new List<DuplicateRow>();

        foreach (var draft in drafts)
        {
            var amount = ResolveSign(draft, fileUsesSignedAmounts);
            var key = $"{draft.Date:yyyy-MM-dd}|{NormalizeDescription(draft.Description)}|{amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

            if (!seen.Add(key))
            {
                duplicates.Add(new DuplicateRow(draft.Line, draft.Description, amount));
                continue;
            }

            var (category, source, keyword) = CategoryCatalog.Match(draft.Description, draft.CategoryText);
            transactions.Add(new Transaction(
                draft.Date,
                draft.Description,
                amount,
                category,
                source,
                keyword,
                draft.Line));
        }

        transactions.Sort(static (a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.LineNumber.CompareTo(b.LineNumber);
        });

        var totals = transactions
            .GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CategoryTotal(
                group.First().Category,
                group.Where(t => t.Amount < 0).Sum(t => -t.Amount),
                group.Where(t => t.Amount > 0).Sum(t => t.Amount),
                group.Count()))
            .OrderByDescending(t => t.Spent)
            .ThenByDescending(t => t.Received)
            .ThenBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalSpent = transactions
            .Where(t => t.Amount < 0 && CategoryCatalog.CountsTowardSpent(t.Category))
            .Sum(t => -t.Amount);

        var totalReceived = transactions
            .Where(t => t.Amount > 0 && CategoryCatalog.CountsTowardReceived(t.Category))
            .Sum(t => t.Amount);

        var habits = HabitGuide.Build(transactions, totalSpent, totalReceived);
        return new SortResult(transactions, totals, issues, duplicates, totalSpent, totalReceived, habits);
    }

    // A minus, parentheses, CR/DR, or debit and credit columns already say which way the money moved.
    // A file of plain positive charges, common on credit-card exports, is treated as spending.
    private static decimal ResolveSign(Draft draft, bool fileUsesSignedAmounts)
    {
        if (draft.Origin is AmountOrigin.Explicit or AmountOrigin.DebitCredit)
            return draft.Amount;

        if (IsCreditType(draft.Type))
            return Math.Abs(draft.Amount);

        if (IsDebitType(draft.Type))
            return -Math.Abs(draft.Amount);

        if (!fileUsesSignedAmounts && !IsMoneyInDescription(draft.Description))
            return -Math.Abs(draft.Amount);

        return draft.Amount;
    }

    private static bool TryReadAmount(string[] fields, ColumnMap map, out AmountRead amount)
    {
        amount = default;

        if (map.Debit is not null || map.Credit is not null)
        {
            var hasDebit = AmountParser.TryParse(Field(fields, map.Debit), out var debit);
            var hasCredit = AmountParser.TryParse(Field(fields, map.Credit), out var credit);
            if (hasDebit || hasCredit)
            {
                var value = (hasCredit ? Math.Abs(credit.Value) : 0m) - (hasDebit ? Math.Abs(debit.Value) : 0m);
                var origin = (hasDebit && debit.ExplicitSign) || (hasCredit && credit.ExplicitSign)
                    ? AmountOrigin.Explicit
                    : AmountOrigin.DebitCredit;
                amount = new AmountRead(value, origin);
                return true;
            }
        }

        if (map.Amount is null || !AmountParser.TryParse(Field(fields, map.Amount), out var parsed))
            return false;

        var plainOrigin = parsed.ExplicitSign ? AmountOrigin.Explicit : AmountOrigin.Plain;
        amount = new AmountRead(parsed.Value, plainOrigin);
        return true;
    }

    private static bool IsRepeatedHeader(string[] fields, ColumnMap map)
    {
        var name = ColumnMapper.Normalize(Field(fields, map.TransactionDate));
        return name is "date" or "transactiondate" or "postingdate" or "posteddate" or "postdate";
    }

    private static string SummaryReason(string[] fields, string dateText)
    {
        var blob = string.Join(' ', fields).Trim();
        if (blob.StartsWith("total", StringComparison.OrdinalIgnoreCase) ||
            blob.StartsWith("balance", StringComparison.OrdinalIgnoreCase) ||
            blob.Contains("total debit", StringComparison.OrdinalIgnoreCase) ||
            blob.Contains("total credit", StringComparison.OrdinalIgnoreCase))
        {
            return "Summary row skipped.";
        }

        if (string.IsNullOrWhiteSpace(dateText))
            return "Missing date.";

        return $"Could not read the date \"{dateText}\".";
    }

    private static bool IsCreditType(string? type)
    {
        var name = NormalizeType(type);
        if (name.Length == 0 || name.Contains("debit", StringComparison.Ordinal))
            return false;

        return name is "credit" or "payment" or "deposit" or "refund" or "return"
            || name.Contains("credit", StringComparison.Ordinal)
            || name.Contains("payment", StringComparison.Ordinal)
            || name.Contains("deposit", StringComparison.Ordinal)
            || name.Contains("refund", StringComparison.Ordinal);
    }

    private static bool IsDebitType(string? type)
    {
        var name = NormalizeType(type);
        if (name.Length == 0 || IsCreditType(type))
            return false;

        return name is "debit" or "sale" or "purchase" or "withdrawal" or "pos" or "fee" or "charge"
            || name.Contains("debit", StringComparison.Ordinal)
            || name.Contains("sale", StringComparison.Ordinal)
            || name.Contains("purchase", StringComparison.Ordinal)
            || name.Contains("withdrawal", StringComparison.Ordinal)
            || name.Contains("fee", StringComparison.Ordinal);
    }

    private static bool IsMoneyInDescription(string description)
    {
        var haystack = description.ToLowerInvariant();
        return haystack.Contains("payroll", StringComparison.Ordinal)
            || haystack.Contains("salary", StringComparison.Ordinal)
            || haystack.Contains("paycheck", StringComparison.Ordinal)
            || haystack.Contains("direct dep", StringComparison.Ordinal)
            || haystack.Contains("refund", StringComparison.Ordinal)
            || haystack.Contains("reimbursement", StringComparison.Ordinal);
    }

    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "";
        return type.Trim().ToLowerInvariant().Replace('_', ' ');
    }

    private static string NormalizeDescription(string description)
    {
        var collapsed = string.Join(' ', description.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed;
    }

    private static string Field(string[] fields, int? index)
    {
        if (index is null || index.Value < 0 || index.Value >= fields.Length)
            return "";
        return fields[index.Value].Trim();
    }

    private static string Preview(string[] fields)
    {
        var raw = string.Join(", ", fields.Select(field => field.Trim()));
        return raw.Length <= 240 ? raw : raw[..240] + "…";
    }

    private enum AmountOrigin
    {
        Plain,
        Explicit,
        DebitCredit,
    }

    private readonly record struct AmountRead(decimal Value, AmountOrigin Origin);

    private sealed class Draft
    {
        public int Line { get; init; }
        public DateOnly Date { get; init; }
        public string Description { get; init; } = "";
        public decimal Amount { get; init; }
        public AmountOrigin Origin { get; init; }
        public string Type { get; init; } = "";
        public string CategoryText { get; init; } = "";
    }
}
