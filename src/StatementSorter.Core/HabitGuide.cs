using System.Globalization;

namespace StatementSorter.Core;

public static class HabitGuide
{
    private static readonly HashSet<string> FixedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Housing",
        "Utilities",
        "Health",
        "Groceries",
        "Transport",
        "Insurance",
    };

    public static HabitReport Build(
        IReadOnlyList<Transaction> transactions,
        decimal totalSpent,
        decimal totalReceived)
    {
        var days = PeriodDays(transactions);
        var leftOver = totalReceived - totalSpent;
        var monthly = MonthlyPace(leftOver, days);
        var spent = SpendingByCategory(transactions);
        var adjustable = spent.Where(row => !IsFixed(row.Category)).ToList();
        var topAdjustable = adjustable.FirstOrDefault();
        var largestFixed = spent.FirstOrDefault(row => IsFixed(row.Category));
        var fees = spent.FirstOrDefault(row => row.Category.Equals("Fees", StringComparison.OrdinalIgnoreCase));
        var insights = new List<HabitInsight>();

        if (days == 0)
        {
            insights.Add(new HabitInsight(
                "No pace yet",
                "This file has no dated transactions, so there isn't a savings pace to estimate."));
        }
        else if (leftOver > 0)
        {
            insights.Add(new HabitInsight(
                "Left to save",
                $"{Money(leftOver)} was left after spending across {Days(days)}. At that pace, about {Money(monthly)} every 30 days can go toward a goal."));
        }
        else if (leftOver < 0)
        {
            insights.Add(new HabitInsight(
                "Spending ran ahead",
                $"Spending was {Money(-leftOver)} more than income over {Days(days)}. Nothing is left for a goal until that gap closes."));
        }
        else
        {
            insights.Add(new HabitInsight(
                "Even for this period",
                $"Spending matched income over {Days(days)}, so this pace doesn't fund a goal yet."));
        }

        if (topAdjustable.Spent > 0)
        {
            var half = decimal.Round(topAdjustable.Spent / 2m, 2, MidpointRounding.AwayFromZero);
            var halfMonthly = MonthlyPace(half, days);
            var share = Share(topAdjustable.Spent, totalSpent);
            var label = topAdjustable.Category.Equals(CategoryCatalog.Uncategorized, StringComparison.OrdinalIgnoreCase)
                ? "Sort the unlabeled charges"
                : $"Adjust {topAdjustable.Category.ToLowerInvariant()}";
            var subject = topAdjustable.Category.Equals(CategoryCatalog.Uncategorized, StringComparison.OrdinalIgnoreCase)
                ? "Unlabeled charges are"
                : $"{topAdjustable.Category} is";

            insights.Add(new HabitInsight(
                label,
                $"{subject} the largest adjustable category, at {Money(topAdjustable.Spent)} ({share} of spending). Cutting it in half would free {Money(half)} in this file, about {Money(halfMonthly)} every 30 days."));
        }
        else if (spent.Count > 0)
        {
            var top = spent[0];
            insights.Add(new HabitInsight(
                $"{top.Category} sets this file",
                $"Dining, shopping, entertainment, and other day-to-day charges didn't show up. {top.Category} is {Money(top.Spent)} and is what this statement is mostly made of."));
        }

        if (largestFixed.Spent > 0 && ShareRatio(largestFixed.Spent, totalSpent) >= 0.5m)
        {
            var adjustableTotal = adjustable.Sum(row => row.Spent);
            var title = IsHousing(largestFixed.Category) ? "Rent sets the floor" : $"{largestFixed.Category} sets the floor";
            insights.Add(new HabitInsight(
                title,
                $"{largestFixed.Category} is {Money(largestFixed.Spent)}, {Share(largestFixed.Spent, totalSpent)} of spending. That bill is hard to move. The adjustable total is {Money(adjustableTotal)}."));
        }
        else if (fees.Spent > 0 && !fees.Category.Equals(topAdjustable.Category, StringComparison.OrdinalIgnoreCase))
        {
            insights.Add(new HabitInsight(
                "Fees first",
                $"Fees cost {Money(fees.Spent)}. Those come off before it makes sense to squeeze groceries or rent."));
        }

        return new HabitReport(days, leftOver, monthly, insights);
    }

    public static decimal MonthlyPace(decimal amount, int periodDays)
    {
        if (periodDays <= 0)
            return 0m;
        return decimal.Round(amount / periodDays * 30m, 2, MidpointRounding.AwayFromZero);
    }

    public static CapStatus MeasureCap(decimal spent, int periodDays, decimal monthlyCap)
    {
        var monthly = MonthlyPace(spent, periodDays);
        var over = decimal.Round(monthly - monthlyCap, 2, MidpointRounding.AwayFromZero);
        return new CapStatus(monthly, over);
    }

    public static decimal StillNeeded(decimal target, decimal alreadySaved)
    {
        if (alreadySaved < 0)
            alreadySaved = 0;
        var left = target - alreadySaved;
        return left <= 0 ? 0 : decimal.Round(left, 2, MidpointRounding.AwayFromZero);
    }

    public static int? MonthsUntil(decimal target, decimal monthlyPace)
    {
        if (target <= 0 || monthlyPace <= 0)
            return null;
        return (int)decimal.Ceiling(target / monthlyPace);
    }

    public static decimal PaceAfterOneOffs(decimal leftOver, decimal oneOffSpent, int periodDays)
    {
        if (oneOffSpent < 0)
            oneOffSpent = 0;
        return MonthlyPace(leftOver + oneOffSpent, periodDays);
    }

    public static string? Around(DateOnly start, decimal needed, decimal monthlyPace)
    {
        if (needed <= 0 || monthlyPace <= 0)
            return null;

        var culture = CultureInfo.GetCultureInfo("en-US");
        var months = needed / monthlyPace;
        if (months < 1m)
        {
            var days = (int)Math.Max(1, decimal.Ceiling(months * 30m));
            if (days < 14)
                return start.AddDays(days).ToString("MMM d, yyyy", culture);

            var weeks = (int)decimal.Ceiling(days / 7m);
            return start.AddDays(weeks * 7).ToString("MMM d, yyyy", culture);
        }

        var whole = (int)decimal.Ceiling(months);
        if (whole >= 24)
        {
            var years = (int)decimal.Ceiling(whole / 12m);
            return start.AddYears(years).ToString("MMMM yyyy", culture);
        }

        return start.AddMonths(whole).ToString("MMMM yyyy", culture);
    }

    private static List<(string Category, decimal Spent)> SpendingByCategory(IReadOnlyList<Transaction> transactions)
    {
        return transactions
            .Where(transaction => transaction.Amount < 0 && CategoryCatalog.CountsTowardSpent(transaction.Category))
            .GroupBy(transaction => transaction.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.First().Category, group.Sum(transaction => -transaction.Amount)))
            .OrderByDescending(row => row.Item2)
            .ToList();
    }

    private static int PeriodDays(IReadOnlyList<Transaction> transactions)
    {
        if (transactions.Count == 0)
            return 0;
        var first = transactions.Min(transaction => transaction.Date);
        var last = transactions.Max(transaction => transaction.Date);
        return last.DayNumber - first.DayNumber + 1;
    }

    private static bool IsFixed(string category)
    {
        if (FixedCategories.Contains(category))
            return true;

        var name = category.ToLowerInvariant();
        return name.Contains("rent", StringComparison.Ordinal)
            || name.Contains("mortgage", StringComparison.Ordinal)
            || name.Contains("utilities", StringComparison.Ordinal)
            || name.Contains("insurance", StringComparison.Ordinal);
    }

    private static bool IsHousing(string category)
    {
        var name = category.ToLowerInvariant();
        return name is "housing" or "rent" || name.Contains("rent", StringComparison.Ordinal) || name.Contains("mortgage", StringComparison.Ordinal);
    }

    private static decimal ShareRatio(decimal part, decimal whole) => whole <= 0 ? 0 : part / whole;

    private static string Share(decimal part, decimal whole)
    {
        var percent = ShareRatio(part, whole) * 100m;
        if (percent <= 0)
            return "0%";
        if (percent < 1)
            return "under 1%";
        return $"about {decimal.Round(percent, 0, MidpointRounding.AwayFromZero):0}%";
    }

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    private static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
}
