using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class StatsWindows
{
    /// <summary>Overall window from tracking start to today; null when today precedes tracking start.</summary>
    public static DateRange? Overall(DateOnly trackingStart, DateOnly today) =>
        today < trackingStart ? null : new DateRange(trackingStart, today);
}
