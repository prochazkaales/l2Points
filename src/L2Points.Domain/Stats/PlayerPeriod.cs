using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class PlayerPeriod
{
    /// <summary>
    /// Range in which a player is expected to take part: from the global tracking start (the same for every
    /// player, regardless of when the account was created) to the earlier of today and deactivation.
    /// Null when that range is empty.
    /// </summary>
    public static DateRange? Resolve(DateOnly trackingStart, DateOnly? deactivatedAt, DateOnly today)
    {
        var end = deactivatedAt ?? today;
        var to = today < end ? today : end;

        return trackingStart <= to ? new DateRange(trackingStart, to) : null;
    }
}
