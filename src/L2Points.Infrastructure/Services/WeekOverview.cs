using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Infrastructure.Services;

/// <summary>Players (ordered by the week's points), attended events of one week and the players' jewels.</summary>
public sealed record WeekOverview(
    Week Week,
    IReadOnlyList<LeaderboardRow> Players,
    IReadOnlyList<WeekEvent> Events,
    JewelsByPlayer Jewels);
