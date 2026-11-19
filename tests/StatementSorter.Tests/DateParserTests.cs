using StatementSorter.Core;

namespace StatementSorter.Tests;

public class DateParserTests
{
    [Theory]
    [InlineData("01/03/2024", "2024-01-03")]
    [InlineData("1/5/24", "2024-01-05")]
    [InlineData("2024-01-07", "2024-01-07")]
    [InlineData("2024/01/07", "2024-01-07")]
    [InlineData("15-Jan-2024", "2024-01-15")]
    [InlineData("Jan 20, 2024", "2024-01-20")]
    [InlineData("January 20, 2024", "2024-01-20")]
    [InlineData("01/18/2024 09:14:02", "2024-01-18")]
    [InlineData("01/15/2024 3:04 PM", "2024-01-15")]
    [InlineData("2024-01-18T09:14:02", "2024-01-18")]
    [InlineData("15/01/2024", "2024-01-15")]
    public void Reads_dates_banks_actually_export(string raw, string expected)
    {
        Assert.True(DateParser.TryParse(raw, out var date));
        Assert.Equal(DateOnly.Parse(expected), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("Total")]
    [InlineData("13/40/2024")]
    [InlineData("01/01/1800")]
    [InlineData("01/01/2200")]
    public void Rejects_values_that_are_not_transaction_dates(string raw)
    {
        Assert.False(DateParser.TryParse(raw, out _));
    }
}
