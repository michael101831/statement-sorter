using StatementSorter.Core;

namespace StatementSorter.Tests;

public class StatementProcessorTests
{
    [Fact]
    public void Sample_statement_totals_spending_and_sets_bad_rows_aside()
    {
        var result = StatementProcessor.Process(SampleStatement.Csv);

        Assert.Equal(10, result.Transactions.Count);
        Assert.Equal(2083.90m, result.TotalSpent);
        Assert.Equal(3250.00m, result.TotalReceived);
        Assert.Equal(3, result.Issues.Count);

        var duplicate = Assert.Single(result.Duplicates);
        Assert.Equal("PAYROLL ACME INC", duplicate.Description);
        Assert.Equal(3250m, duplicate.Amount);
        Assert.Single(result.Transactions, transaction => transaction.Description == "PAYROLL ACME INC");

        var amazon = Assert.Single(result.Transactions, transaction => transaction.Description.StartsWith("AMAZON", StringComparison.Ordinal));
        Assert.Equal("AMAZON.COM*AB1CD2, SEATTLE", amazon.Description);
        Assert.Equal(-42.18m, amazon.Amount);
        Assert.Equal(new DateOnly(2024, 1, 7), amazon.Date);
        Assert.Equal("Shopping", amazon.Category);

        var eats = Assert.Single(result.Transactions, transaction => transaction.Description.Contains("UBER EATS", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2024, 1, 20), eats.Date);
        Assert.Equal("Dining", eats.Category);
        Assert.Equal("uber eats", eats.MatchedKeyword);

        var coffee = Assert.Single(result.Transactions, transaction => transaction.Description.StartsWith("STARBUCKS", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2024, 1, 5), coffee.Date);

        var netflix = Assert.Single(result.Transactions, transaction => transaction.Description.StartsWith("NETFLIX", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2024, 1, 15), netflix.Date);

        var shell = Assert.Single(result.Transactions, transaction => transaction.Description.StartsWith("SHELL", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2024, 1, 18), shell.Date);
        Assert.Equal(-48.20m, shell.Amount);
        Assert.Equal("Transport", shell.Category);

        var cvs = Assert.Single(result.Transactions, transaction => transaction.Description.Contains("CVS", StringComparison.Ordinal));
        Assert.Equal("Pharmacy", cvs.Category);
        Assert.Equal("column", cvs.Source);
        Assert.Equal(-12.06m, cvs.Amount);

        var housing = Assert.Single(result.Totals, total => total.Category == "Housing");
        Assert.Equal(1850m, housing.Spent);

        Assert.Contains(result.Issues, issue => issue.Reason.Contains("not-a-date", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Reason == "Missing amount.");
        Assert.Contains(result.Issues, issue => issue.Reason == "Summary row skipped.");
        Assert.DoesNotContain(result.Transactions, transaction => transaction.Description.Contains("UNKNOWN", StringComparison.Ordinal));
    }

    [Fact]
    public void Quoted_comma_and_parentheses_stay_one_transaction()
    {
        const string csv =
            """
            Date,Description,Amount
            "Jan 20, 2024","AMAZON.COM*AB1CD2, SEATTLE","($1,234.56)"
            """;

        var result = StatementProcessor.Process(csv);
        var transaction = Assert.Single(result.Transactions);

        Assert.Equal(new DateOnly(2024, 1, 20), transaction.Date);
        Assert.Equal("AMAZON.COM*AB1CD2, SEATTLE", transaction.Description);
        Assert.Equal(-1234.56m, transaction.Amount);
        Assert.Equal("Shopping", transaction.Category);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Chase_checking_export_keeps_signed_amounts()
    {
        const string csv =
            """
            Details,Posting Date,Description,Amount,Type,Balance,Check or Slip #
            DEBIT,01/15/2024,AMAZON.COM,-45.67,ACH_DEBIT,1000.00,
            CREDIT,01/16/2024,PAYROLL ACME,2500.00,ACH_CREDIT,3500.00,
            """;

        var result = StatementProcessor.Process(csv);

        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal(-45.67m, result.Transactions[0].Amount);
        Assert.Equal("Shopping", result.Transactions[0].Category);
        Assert.Equal(2500m, result.Transactions[1].Amount);
        Assert.Equal("Income", result.Transactions[1].Category);
        Assert.Equal(45.67m, result.TotalSpent);
        Assert.Equal(2500m, result.TotalReceived);
    }

    [Fact]
    public void Debit_and_credit_columns_set_the_sign_and_skip_duplicates()
    {
        const string csv =
            """
            Transaction Date,Description,Debit,Credit
            01/04/2024,TARGET STORE,54.10,
            01/05/2024,PAYCHECK,,2100.00
            01/05/2024,PAYCHECK,,2100.00
            """;

        var result = StatementProcessor.Process(csv);

        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal(-54.10m, result.Transactions[0].Amount);
        Assert.Equal("Shopping", result.Transactions[0].Category);
        Assert.Equal(2100m, result.Transactions[1].Amount);
        Assert.Equal("Income", result.Transactions[1].Category);
        Assert.Single(result.Duplicates);
    }

    [Fact]
    public void Positive_credit_card_charges_count_as_spending_and_refunds_do_not()
    {
        const string csv =
            """
            Date,Description,Amount
            01/02/2024,NETFLIX.COM,15.49
            01/03/2024,REFUND AMAZON,12.00
            """;

        var result = StatementProcessor.Process(csv);

        Assert.Equal(-15.49m, result.Transactions[0].Amount);
        Assert.Equal("Entertainment", result.Transactions[0].Category);
        Assert.Equal(12m, result.Transactions[1].Amount);
        Assert.Equal(15.49m, result.TotalSpent);
        Assert.Equal(12m, result.TotalReceived);
    }

    [Fact]
    public void Category_column_wins_over_the_description_rule()
    {
        const string csv =
            """
            Date,Description,Category,Amount
            01/01/2024,AMAZON,Groceries,-10.00
            """;

        var result = StatementProcessor.Process(csv);
        var transaction = Assert.Single(result.Transactions);

        Assert.Equal("Groceries", transaction.Category);
        Assert.Equal("column", transaction.Source);
    }

    [Fact]
    public void Duplicate_rows_are_skipped_when_spacing_and_case_differ()
    {
        const string csv =
            """
            Date,Description,Amount
            01/01/2024,STARBUCKS  STORE,-5.00
            01/02/2024,SHELL,-20.00
            01/01/2024, starbucks store ,-5.00
            """;

        var result = StatementProcessor.Process(csv);

        Assert.Equal(2, result.Transactions.Count);
        var duplicate = Assert.Single(result.Duplicates);
        Assert.Equal(4, duplicate.LineNumber);
        Assert.Equal(-5m, duplicate.Amount);
    }

    [Fact]
    public void Semicolon_exports_still_sort()
    {
        const string csv =
            """
            Date;Description;Amount
            01/02/2024;SHELL;-20.00
            """;

        var result = StatementProcessor.Process(csv);
        var transaction = Assert.Single(result.Transactions);

        Assert.Equal(-20m, transaction.Amount);
        Assert.Equal("Transport", transaction.Category);
    }

    [Fact]
    public void Leading_bom_does_not_hide_the_header()
    {
        var csv = "\uFEFFDate,Description,Amount\n01/01/2024,SHELL,-10.00\n";
        var result = StatementProcessor.Process(csv);

        Assert.Single(result.Transactions);
    }

    [Fact]
    public void A_bad_row_does_not_throw_away_the_rest_of_the_file()
    {
        const string csv =
            """
            Date,Description,Amount
            01/02/2024,SHELL,-20.00
            not-a-date,UNKNOWN,-3.00
            01/03/2024,NETFLIX.COM,-15.49
            """;

        var result = StatementProcessor.Process(csv);

        Assert.Equal(2, result.Transactions.Count);
        var issue = Assert.Single(result.Issues);
        Assert.Contains("not-a-date", issue.Reason, StringComparison.Ordinal);
        Assert.Equal(35.49m, result.TotalSpent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Empty_files_are_rejected(string csv)
    {
        var error = Assert.Throws<StatementFormatException>(() => StatementProcessor.Process(csv));
        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Files_without_a_date_and_amount_header_are_rejected()
    {
        const string csv =
            """
            hello,world
            1,2
            """;

        var error = Assert.Throws<StatementFormatException>(() => StatementProcessor.Process(csv));
        Assert.Contains("header", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
