using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Services;

/// <summary>
/// Leaderboard rows together with the jewels of all players and the total points of the same players in the week
/// before the shown one (null when that week ended before tracking started).
/// </summary>
public sealed record LeaderboardResult(IReadOnlyList<LeaderboardRow> Rows, JewelsByPlayer Jewels, int? PreviousWeekPoints);
