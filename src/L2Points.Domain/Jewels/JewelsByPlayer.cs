namespace L2Points.Domain.Jewels;

/// <summary>Jewels of each player, grouped by type and ordered by the type's sort order.</summary>
public sealed class JewelsByPlayer
{
    public static readonly JewelsByPlayer Empty = new(new Dictionary<string, IReadOnlyList<PlayerJewel>>());

    private readonly IReadOnlyDictionary<string, IReadOnlyList<PlayerJewel>> _byUser;

    internal JewelsByPlayer(IReadOnlyDictionary<string, IReadOnlyList<PlayerJewel>> byUser) => _byUser = byUser;

    /// <summary>Jewels of the player; empty for an unknown id.</summary>
    public IReadOnlyList<PlayerJewel> Of(string userId) =>
        _byUser.TryGetValue(userId, out var jewels) ? jewels : [];
}
