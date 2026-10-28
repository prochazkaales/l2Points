namespace L2Points.Domain.Jewels;

/// <summary>Party-wide piece count of one jewel type and who holds it.</summary>
public sealed record JewelTypeSummary(JewelType Type, int Count, IReadOnlyList<JewelHolder> Holders);
