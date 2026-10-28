using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Infrastructure.Services;

/// <summary>Players (ordered by the week's points) and attended events of one week.</summary>
public sealed record WeekOverview(Week Week, IReadOnlyList<LeaderboardRow> Players, IReadOnlyList<WeekEvent> Events);
