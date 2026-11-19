using System.Globalization;
using StatementSorter.Core;

namespace StatementSorter.Tests;

public class AmountParserTests
{
    [Theory]
    [InlineData("(42.18)", "-42.18", true)]
    [InlineData("($1,234.56)", "-1234.56", true)]
    [InlineData("-1,850.00", "-1850", true)]
    [InlineData("45.67 DR", "-45.67", true)]
    [InlineData("12.00-", "-12", true)]
    [InlineData("45.67 CR", "45.67", true)]
    [InlineData("$1,234.56", "1234.56", false)]
    [InlineData("3,250.00", "3250", false)]
    [InlineData("40.00", "40", false)]
    public void Reads_amounts_banks_actually_export(string raw, string expected, bool explicitSign)
    {
        Assert.True(AmountParser.TryParse(raw, out var amount));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), amount.Value);
        Assert.Equal(explicitSign, amount.ExplicitSign);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("abc")]
    [InlineData("CR")]
    public void Rejects_values_that_are_not_amounts(string raw)
    {
        Assert.False(AmountParser.TryParse(raw, out _));
    }
}
