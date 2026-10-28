namespace L2Points.Domain.Stats;

/// <summary>Points and participation of one player over a window.</summary>
public sealed record PlayerSummary(int Points, ParticipationStats Stats);
