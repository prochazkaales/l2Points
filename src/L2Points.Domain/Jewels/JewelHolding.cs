namespace L2Points.Domain.Jewels;

/// <summary>One owned jewel piece, as input to the jewel calculators.</summary>
public sealed record JewelHolding(string UserId, int JewelTypeId);
