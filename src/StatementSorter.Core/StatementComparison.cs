namespace StatementSorter.Core;

public static class StatementComparison
{
    public static IReadOnlyList<CategoryChange> Changes(SortResult later, SortResult earlier)
    {
        var now = Paces(later);
        var then = Paces(earlier);
        var names = new HashSet<string>(now.Keys, StringComparer.OrdinalIgnoreCase);
        names.UnionWith(then.Keys);

        var changes = new List<CategoryChange>();
        foreach (var name in names)
        {
            now.TryGetValue(name, out var currentPace);
            then.TryGetValue(name, out var previousPace);
            var delta = decimal.Round(currentPace - previousPace, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(delta) < 1m)
                continue;

            var display = DisplayName(later, name) ?? DisplayName(earlier, name) ?? name;
            changes.Add(new CategoryChange(display, currentPace, previousPace, delta));
        }

        return changes
            .OrderByDescending(change => Math.Abs(change.Delta))
            .ThenBy(change => change.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, decimal> Paces(SortResult result)
    {
        var paces = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var total in result.Totals)
        {
            if (total.Spent <= 0 || !CategoryCatalog.CountsTowardSpent(total.Category))
                continue;
            paces[total.Category] = HabitGuide.MonthlyPace(total.Spent, result.Habits.PeriodDays);
        }

        return paces;
    }

    private static string? DisplayName(SortResult result, string category)
    {
        return result.Totals.FirstOrDefault(total => total.Category.Equals(category, StringComparison.OrdinalIgnoreCase))?.Category;
    }
}
