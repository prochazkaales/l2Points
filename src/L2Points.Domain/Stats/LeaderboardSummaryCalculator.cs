namespace L2Points.Domain.Stats;

public static class LeaderboardSummaryCalculator
{
    private const int PodiumSize = 3;

    /// <summary>
    /// Figures over the given rows, which must be ordered by overall points descending (as <see cref="LeaderboardCalculator"/> returns them).
    /// Ranks are shared on equal points (1, 2, 2, 4).
    /// </summary>
    public static LeaderboardSummary Calculate(IReadOnlyList<LeaderboardRow> rows, string? userId)
    {
        var ranks = Ranks(rows, r => r.Overall.Points);
        var partyAttendance = rows.Aggregate(default(CategoryStat), (sum, r) => sum.Add(r.Overall.Stats.Total));
        var weekPoints = rows.Sum(r => r.Week.Points);
        var bestWeek = rows.Count == 0 ? 0 : rows.Max(r => r.Week.Points);
        var playersOfWeek = bestWeek > 0
            ? rows.Where(r => r.Week.Points == bestWeek).Select(r => r.Player).ToList()
            : [];
        var podium = rows.Where(r => r.Overall.Points > 0).Take(PodiumSize).ToList();

        return new LeaderboardSummary(ranks, partyAttendance, weekPoints, playersOfWeek, bestWeek, podium, Standing(rows, ranks, userId));
    }

    /// <summary>Place of each player by the given points, shared on equal points (1, 2, 2, 4); the order of rows does not matter.</summary>
    public static IReadOnlyDictionary<string, int> Ranks(IReadOnlyCollection<LeaderboardRow> rows, Func<LeaderboardRow, int> points) =>
        rows.ToDictionary(r => r.Player.UserId, r => 1 + rows.Count(other => points(other) > points(r)));

    private static PlayerStanding? Standing(IReadOnlyList<LeaderboardRow> rows, IReadOnlyDictionary<string, int> ranks, string? userId)
    {
        var me = userId is null ? null : rows.FirstOrDefault(r => r.Player.UserId == userId);
        if (me is null)
        {
            return null;
        }

        var points = me.Overall.Points;
        var shared = rows.Count(r => r.Overall.Points == points) > 1;
        var higher = rows.Where(r => r.Overall.Points > points).ToList();
        var lower = rows.Where(r => r.Overall.Points < points).ToList();
        int? behind = higher.Count > 0 ? higher.Min(r => r.Overall.Points) - points : null;
        int? ahead = lower.Count > 0 ? points - lower.Max(r => r.Overall.Points) : null;
        return new PlayerStanding(ranks[userId!], shared, behind, ahead);
    }
}
