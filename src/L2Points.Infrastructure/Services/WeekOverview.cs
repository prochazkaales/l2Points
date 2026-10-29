using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Infrastructure.Services;

/// <summary>
/// Players (ordered by the week's points), attended events of one week, the players' jewels and the total points
/// of the previous week (null when that week ended before tracking started).
/// </summary>
public sealed record WeekOverview(
    Week Week,
    IReadOnlyList<LeaderboardRow> Players,
    IReadOnlyList<WeekEvent> Events,
    JewelsByPlayer Jewels,
    int? PreviousWeekPoints);
