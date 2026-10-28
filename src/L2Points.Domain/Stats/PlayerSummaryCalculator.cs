using L2Points.Domain.Activities;
using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class PlayerSummaryCalculator
{
    /// <summary>
    /// Points over attendances dated within <paramref name="window"/>; participation over the part of
    /// <paramref name="playerPeriod"/> that falls into the window (empty when there is none).
    /// </summary>
    public static PlayerSummary Calculate(
        IReadOnlyList<Activity> activities,
        IReadOnlyCollection<AttendanceEntry> attendances,
        DateRange window,
        DateRange? playerPeriod)
    {
        var points = PointsCalculator.Sum(activities, attendances.Where(a => window.Contains(a.Date)));
        var stats = ParticipationCalculator.Calculate(activities, attendances, playerPeriod?.Intersect(window));
        return new PlayerSummary(points, stats);
    }
}
