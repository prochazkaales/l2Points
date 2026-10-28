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

    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    /// <summary>
    /// Week containing the <c>yyyy-MM-dd</c> date, or the week containing <paramref name="today"/> when it cannot be parsed
    /// or its year is outside 2000..2100 (guards against date overflow in <see cref="Sunday"/> and <see cref="Next"/>).
    /// </summary>
    public static Week ParseOrCurrent(string? isoDate, DateOnly today) =>
        DateOnly.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date.Year is >= MinYear and <= MaxYear
            ? Containing(date)
            : Containing(today);

    public DateOnly Sunday => Monday.AddDays(6);

    public IEnumerable<DateOnly> Days => Enumerable.Range(0, 7).Select(Monday.AddDays);

    public Week Next() => new(Monday.AddDays(7));

    public Week Previous() => new(Monday.AddDays(-7));

    public DateRange Range => new(Monday, Sunday);
}
