using L2Points.Domain.Activities;
using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class LeaderboardCalculator
{
    /// <summary>
    /// One row per given player (filtering out inactive players is the caller's job), ordered by overall
    /// points descending and then by nick ignoring case and finally by user id (fully deterministic).
    /// </summary>
    public static IReadOnlyList<LeaderboardRow> Build(
        IReadOnlyList<PlayerInfo> players,
        IReadOnlyList<Activity> activities,
        IReadOnlyCollection<UserAttendance> attendances,
        Week week,
        DateOnly trackingStart,
        DateOnly today)
    {
        var overallWindow = StatsWindows.Overall(trackingStart, today);
        var byUser = attendances
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AttendanceEntry>)g.Select(a => a.Entry).ToList());

        return players
            .Select(player =>
            {
                IReadOnlyCollection<AttendanceEntry> entries = byUser.GetValueOrDefault(player.UserId) ?? [];
                var period = PlayerPeriod.Resolve(trackingStart, player.ActiveFrom, player.DeactivatedAt, today);
                var overall = overallWindow is { } window
                    ? PlayerSummaryCalculator.Calculate(activities, entries, window, period)
                    : PlayerSummary.Empty;
                var weekly = PlayerSummaryCalculator.Calculate(activities, entries, week.Range, period);
                return new LeaderboardRow(player, overall, weekly);
            })
            .OrderByDescending(r => r.Overall.Points)
            .ThenBy(r => r.Player.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Player.UserId, StringComparer.Ordinal)
            .ToList();
    }
}
