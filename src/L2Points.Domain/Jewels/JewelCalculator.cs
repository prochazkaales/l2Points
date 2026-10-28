using L2Points.Domain.Stats;

namespace L2Points.Domain.Jewels;

public static class JewelCalculator
{
    /// <summary>Groups holdings per player by type, in type order. Holdings of unknown types are ignored.</summary>
    public static JewelsByPlayer ByPlayer(IReadOnlyList<JewelType> types, IEnumerable<JewelHolding> holdings)
    {
        var ordered = Ordered(types);
        var byId = ordered.ToDictionary(t => t.Id);

        var byUser = holdings
            .Where(h => byId.ContainsKey(h.JewelTypeId))
            .GroupBy(h => h.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var counts = g.CountBy(h => h.JewelTypeId).ToDictionary(c => c.Key, c => c.Value);
                    return (IReadOnlyList<PlayerJewel>)ordered
                        .Where(t => counts.ContainsKey(t.Id))
                        .Select(t => new PlayerJewel(t, counts[t.Id]))
                        .ToList();
                });

        return new JewelsByPlayer(byUser);
    }

    /// <summary>
    /// Per-type piece counts across the party and their holders (including inactive players), all types in order.
    /// Holdings of players missing from <paramref name="players"/> are ignored.
    /// </summary>
    public static IReadOnlyList<JewelTypeSummary> Gallery(
        IReadOnlyList<JewelType> types, IEnumerable<JewelHolding> holdings, IReadOnlyList<PlayerInfo> players)
    {
        var playersById = players.ToDictionary(p => p.UserId);
        var known = holdings.Where(h => playersById.ContainsKey(h.UserId)).ToList();

        return Ordered(types)
            .Select(type =>
            {
                var holders = known
                    .Where(h => h.JewelTypeId == type.Id)
                    .CountBy(h => h.UserId)
                    .Select(c => new JewelHolder(playersById[c.Key], c.Value))
                    .OrderBy(h => h.Player.Nick, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(h => h.Player.UserId, StringComparer.Ordinal)
                    .ToList();

                return new JewelTypeSummary(type, holders.Sum(h => h.Count), holders);
            })
            .ToList();
    }

    private static List<JewelType> Ordered(IReadOnlyList<JewelType> types) =>
        types.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
}
