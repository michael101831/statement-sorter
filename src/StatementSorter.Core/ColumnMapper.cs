namespace StatementSorter.Core;

public sealed class ColumnMap
{
    public int? TransactionDate { get; init; }
    public int? Description { get; init; }
    public int? Amount { get; init; }
    public int? Debit { get; init; }
    public int? Credit { get; init; }
    public int? Category { get; init; }
    public int? Type { get; init; }

    public bool IsUsable => TransactionDate is not null && (Amount is not null || Debit is not null || Credit is not null);
}

public static class ColumnMapper
{
    public static ColumnMap? TryMap(IReadOnlyList<string> header)
    {
        int? date = null;
        int? posted = null;
        int? description = null;
        int? memo = null;
        int? amount = null;
        int? debit = null;
        int? credit = null;
        int? category = null;
        int? type = null;
        int? details = null;

        for (var i = 0; i < header.Count; i++)
        {
            var name = Normalize(header[i]);
            if (name.Length == 0)
                continue;

            switch (name)
            {
                case "date":
                case "transactiondate":
                case "transdate":
                case "purchasedate":
                case "valuedate":
                    date ??= i;
                    break;
                case "postingdate":
                case "posteddate":
                case "postdate":
                case "bookingdate":
                    posted ??= i;
                    break;
                case "description":
                case "transactiondescription":
                case "merchantdescription":
                case "merchant":
                case "payee":
                case "name":
                case "narrative":
                    description ??= i;
                    break;
                case "memo":
                case "note":
                    memo ??= i;
                    break;
                case "amount":
                case "amt":
                case "transactionamount":
                    amount ??= i;
                    break;
                case "debit":
                case "debitamount":
                case "withdrawal":
                case "withdrawalamount":
                case "moneyout":
                    debit ??= i;
                    break;
                case "credit":
                case "creditamount":
                case "deposit":
                case "depositamount":
                case "moneyin":
                    credit ??= i;
                    break;
                case "category":
                case "categories":
                    category ??= i;
                    break;
                case "type":
                case "transactiontype":
                    type ??= i;
                    break;
                case "details":
                    details ??= i;
                    break;
                default:
                    if (name.StartsWith("amount", StringComparison.Ordinal))
                        amount ??= i;
                    else if (name.StartsWith("debit", StringComparison.Ordinal))
                        debit ??= i;
                    else if (name.StartsWith("credit", StringComparison.Ordinal) &&
                             !name.Contains("limit", StringComparison.Ordinal) &&
                             !name.Contains("score", StringComparison.Ordinal))
                        credit ??= i;
                    else if (name.Contains("description", StringComparison.Ordinal))
                        description ??= i;
                    else if (name.Contains("date", StringComparison.Ordinal) &&
                             !name.Contains("update", StringComparison.Ordinal))
                    {
                        if (name.Contains("post", StringComparison.Ordinal))
                            posted ??= i;
                        else
                            date ??= i;
                    }
                    break;
            }
        }

        var map = new ColumnMap
        {
            TransactionDate = date ?? posted,
            Description = description ?? memo ?? details,
            Amount = amount,
            Debit = debit,
            Credit = credit,
            Category = category,
            Type = type ?? (description is not null ? details : null),
        };

        return map.IsUsable ? map : null;
    }

    public static string Normalize(string header)
    {
        var chars = header.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }
}
