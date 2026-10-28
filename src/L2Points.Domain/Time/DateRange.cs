namespace L2Points.Domain.Time;

/// <summary>Inclusive range of dates, <see cref="From"/> &lt;= <see cref="To"/>.</summary>
public readonly record struct DateRange
{
    public DateRange(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new ArgumentException($"Range start {from} is after its end {to}.", nameof(from));

        From = from;
        To = to;
    }

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly d) => d >= From && d <= To;

    public DateRange? Intersect(DateRange other)
    {
        var from = From > other.From ? From : other.From;
        var to = To < other.To ? To : other.To;
        return from <= to ? new DateRange(from, to) : null;
    }

    /// <summary>All weeks the range touches, even partially.</summary>
    public IEnumerable<Week> Weeks
    {
        get
        {
            var last = Week.Containing(To);
            for (var week = Week.Containing(From); week.Monday <= last.Monday; week = week.Next())
                yield return week;
        }
    }
}
