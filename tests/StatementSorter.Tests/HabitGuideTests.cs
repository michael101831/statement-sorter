using StatementSorter.Core;

namespace StatementSorter.Tests;

public class HabitGuideTests
{
    [Fact]
    public void Sample_statement_shows_what_is_left_and_what_to_adjust()
    {
        var result = StatementProcessor.Process(SampleStatement.Csv);
        var habits = result.Habits;

        Assert.Equal(31, habits.PeriodDays);
        Assert.Equal(1166.10m, habits.LeftOver);
        Assert.Equal(1128.48m, habits.MonthlyPace);
        Assert.Equal(5, HabitGuide.MonthsUntil(5000m, habits.MonthlyPace));

        Assert.Equal("Left to save", habits.Insights[0].Title);
        Assert.Contains("$1,166.10", habits.Insights[0].Detail, StringComparison.Ordinal);
        Assert.Contains("$1,128.48", habits.Insights[0].Detail, StringComparison.Ordinal);

        var shopping = Assert.Single(habits.Insights, insight => insight.Title == "Adjust shopping");
        Assert.Contains("$42.18", shopping.Detail, StringComparison.Ordinal);
        Assert.Contains("Cutting it in half", shopping.Detail, StringComparison.Ordinal);

        var rent = Assert.Single(habits.Insights, insight => insight.Title == "Rent sets the floor");
        Assert.Contains("89%", rent.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Overspending_names_the_habit_and_does_not_invent_a_timeline()
    {
        const string csv =
            """
            Date,Description,Amount
            01/01/2024,STARBUCKS,-40.00
            01/10/2024,UBER EATS,-20.00
            """;

        var habits = StatementProcessor.Process(csv).Habits;

        Assert.Equal(10, habits.PeriodDays);
        Assert.Equal(-60m, habits.LeftOver);
        Assert.True(habits.MonthlyPace < 0);
        Assert.Contains(habits.Insights, insight => insight.Title == "Adjust dining");
        Assert.Null(HabitGuide.MonthsUntil(100m, habits.MonthlyPace));
    }

    [Fact]
    public void Rent_from_the_file_is_treated_as_a_bill_not_a_habit()
    {
        const string csv =
            """
            Date,Description,Category,Amount
            03/01/2024,MARCH RENT,Rent,-1400.00
            03/02/2024,NETFLIX.COM,,-15.49
            03/15/2024,PAYROLL,Income,2000.00
            """;

        var habits = StatementProcessor.Process(csv).Habits;

        Assert.Contains(habits.Insights, insight => insight.Title == "Adjust entertainment");
        Assert.Contains(habits.Insights, insight => insight.Title == "Rent sets the floor");
        Assert.DoesNotContain(habits.Insights, insight => insight.Title.Contains("rent", StringComparison.OrdinalIgnoreCase) && insight.Title.StartsWith("Adjust", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1128.48", 1)]
    [InlineData("1128.49", 2)]
    [InlineData("5000", 5)]
    public void Months_until_a_goal_rounds_up(string target, int months)
    {
        Assert.Equal(months, HabitGuide.MonthsUntil(decimal.Parse(target, System.Globalization.CultureInfo.InvariantCulture), 1128.48m));
    }

    [Fact]
    public void A_cap_measures_the_monthly_gap()
    {
        var dining = StatementProcessor.Process(SampleStatement.Csv).Totals.Single(total => total.Category == "Dining");
        var status = HabitGuide.MeasureCap(dining.Spent, 31, 20m);

        Assert.Equal(29.55m, dining.Spent);
        Assert.Equal(28.60m, status.MonthlySpent);
        Assert.Equal(8.60m, status.OverBy);
    }

    [Fact]
    public void Money_already_saved_shortens_what_is_left()
    {
        Assert.Equal(4000m, HabitGuide.StillNeeded(5000m, 1000m));
        Assert.Equal(0m, HabitGuide.StillNeeded(500m, 800m));
        Assert.Equal(4, HabitGuide.MonthsUntil(HabitGuide.StillNeeded(5000m, 1000m), 1128.48m));
    }

    [Fact]
    public void The_earlier_sample_shows_dining_down_and_shopping_up()
    {
        var later = StatementProcessor.Process(SampleStatement.Csv);
        var earlier = StatementProcessor.Process(SampleStatement.EarlierCsv);
        var changes = StatementComparison.Changes(later, earlier);

        var dining = Assert.Single(changes, change => change.Category == "Dining");
        var shopping = Assert.Single(changes, change => change.Category == "Shopping");

        Assert.Equal(-48.82m, dining.Delta);
        Assert.Equal(26.30m, shopping.Delta);
        Assert.DoesNotContain(changes, change => change.Category == "Housing");
    }

    [Fact]
    public void A_non_positive_pace_has_no_savings_timeline()
    {
        Assert.Null(HabitGuide.MonthsUntil(100m, 0m));
        Assert.Null(HabitGuide.MonthsUntil(100m, -20m));
        Assert.Null(HabitGuide.MonthsUntil(0m, 100m));
    }
}
