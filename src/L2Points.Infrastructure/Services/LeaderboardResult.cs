using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Services;

/// <summary>Leaderboard rows together with the jewels of all players.</summary>
public sealed record LeaderboardResult(IReadOnlyList<LeaderboardRow> Rows, JewelsByPlayer Jewels);
