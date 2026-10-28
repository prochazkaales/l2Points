using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Services;

/// <summary>Everything the admin assignment page needs: all players (including inactive), all types and who holds what.</summary>
public sealed record JewelAssignments(IReadOnlyList<PlayerInfo> Players, IReadOnlyList<JewelType> Types, JewelsByPlayer Jewels);
