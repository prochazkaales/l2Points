using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Services;

/// <summary>Overall, current-week and per-week history statistics of one player, plus the jewels they hold.</summary>
public sealed record PlayerDetail(
    PlayerInfo Player,
    PlayerSummary Overall,
    PlayerSummary CurrentWeek,
    IReadOnlyList<WeekHistoryRow> History,
    IReadOnlyList<PlayerJewel> Jewels);
