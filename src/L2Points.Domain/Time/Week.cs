using System.Globalization;

namespace L2Points.Domain.Time;

/// <summary>ISO week (Monday to Sunday), identified by its Monday.</summary>
public readonly record struct Week(DateOnly Monday)
{
    public static Week Containing(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new Week(date.AddDays(-daysSinceMonday));
    }

    /// <summary>Week containing the <c>yyyy-MM-dd</c> date, or the week containing <paramref name="today"/> when it cannot be parsed.</summary>
    public static Week ParseOrCurrent(string? isoDate, DateOnly today) =>
        DateOnly.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Containing(date)
            : Containing(today);

    public DateOnly Sunday => Monday.AddDays(6);

    public IEnumerable<DateOnly> Days => Enumerable.Range(0, 7).Select(Monday.AddDays);

    public Week Next() => new(Monday.AddDays(7));

    public Week Previous() => new(Monday.AddDays(-7));

    public DateRange Range => new(Monday, Sunday);
}
