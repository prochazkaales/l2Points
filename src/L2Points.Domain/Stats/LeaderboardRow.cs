namespace L2Points.Domain.Stats;

public sealed record LeaderboardRow(PlayerInfo Player, PlayerSummary Overall, PlayerSummary Week);
