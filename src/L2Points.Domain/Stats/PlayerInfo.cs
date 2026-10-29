namespace L2Points.Domain.Stats;

/// <summary>Player data needed by the statistics calculators.</summary>
public sealed record PlayerInfo(string UserId, string Nick, bool IsActive, DateOnly ActiveFrom, DateOnly? DeactivatedAt);
