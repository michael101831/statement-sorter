namespace StatementSorter.Core;

public sealed record Transaction(
    DateOnly Date,
    string Description,
    decimal Amount,
    string Category,
    string Source,
    string? MatchedKeyword,
    int LineNumber);

public sealed record CategoryTotal(string Category, decimal Spent, decimal Received, int Count);

public sealed record RowIssue(int LineNumber, string Reason, string Raw);

public sealed record DuplicateRow(int LineNumber, string Description, decimal Amount);

public sealed record HabitInsight(string Title, string Detail);

public sealed record HabitReport(
    int PeriodDays,
    decimal LeftOver,
    decimal MonthlyPace,
    IReadOnlyList<HabitInsight> Insights);

public readonly record struct CapStatus(decimal MonthlySpent, decimal OverBy);

public sealed record CategoryChange(string Category, decimal CurrentPace, decimal PreviousPace, decimal Delta);

public sealed record SortResult(
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<CategoryTotal> Totals,
    IReadOnlyList<RowIssue> Issues,
    IReadOnlyList<DuplicateRow> Duplicates,
    decimal TotalSpent,
    decimal TotalReceived,
    HabitReport Habits);

public sealed class StatementFormatException : Exception
{
    public StatementFormatException(string message) : base(message)
    {
    }
}
