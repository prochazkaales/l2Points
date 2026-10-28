using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public sealed record WeekHistoryRow(Week Week, PlayerSummary Summary);
