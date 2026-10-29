namespace L2Points.Domain.Stats;

/// <summary>Dashboard figures above the leaderboard, computed over the rows it shows.</summary>
public sealed record LeaderboardSummary(
    IReadOnlyDictionary<string, int> Ranks,
    CategoryStat PartyAttendance,
    CategoryStat WeekAttendance,
    int WeekPoints,
    IReadOnlyList<PlayerInfo> PlayersOfWeek,
    int PlayersOfWeekPoints,
    IReadOnlyList<LeaderboardRow> Podium,
    PlayerStanding? Me);

/// <summary>
/// Overall place of one player. <paramref name="Shared"/> means another player has the same points;
/// <paramref name="Behind"/> is the gap to the nearest higher score, <paramref name="Ahead"/> the lead over the nearest lower one.
/// </summary>
public sealed record PlayerStanding(int Rank, bool Shared, int? Behind, int? Ahead);
