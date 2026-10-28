using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Services;

/// <summary>Overall, current-week and per-week history statistics of one player.</summary>
public sealed record PlayerDetail(
    PlayerInfo Player,
    PlayerSummary Overall,
    PlayerSummary CurrentWeek,
    IReadOnlyList<WeekHistoryRow> History);
