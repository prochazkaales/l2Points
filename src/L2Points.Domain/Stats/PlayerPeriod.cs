using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class PlayerPeriod
{
    /// <summary>
    /// Range in which a player is expected to take part: from the later of tracking start and activation,
    /// to the earlier of today and deactivation. Null when that range is empty.
    /// </summary>
    public static DateRange? Resolve(DateOnly trackingStart, DateOnly activeFrom, DateOnly? deactivatedAt, DateOnly today)
    {
        var from = trackingStart > activeFrom ? trackingStart : activeFrom;
        var end = deactivatedAt ?? today;
        var to = today < end ? today : end;

        return from <= to ? new DateRange(from, to) : null;
    }
}
