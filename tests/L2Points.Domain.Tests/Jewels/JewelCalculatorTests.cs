using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;

namespace L2Points.Domain.Tests.Jewels;

public class JewelCalculatorTests
{
    private static readonly JewelType Ring = new() { Id = 10, Code = "Ring", Name = "Ring", SortOrder = 1 };
    private static readonly JewelType Earring = new() { Id = 20, Code = "Earring", Name = "Earring", SortOrder = 2 };
    private static readonly JewelType Necklace = new() { Id = 30, Code = "Necklace", Name = "Necklace", SortOrder = 3 };

    // Deliberately unordered to prove the calculator orders by SortOrder.
    private static readonly IReadOnlyList<JewelType> Types = [Necklace, Ring, Earring];

    private static readonly PlayerInfo Bob = new("bob", "bob", true, null);
    private static readonly PlayerInfo Alice = new("alice", "Alice", true, null);
    private static readonly PlayerInfo Carl = new("carl", "carl", false, new DateOnly(2026, 11, 1));

    [Fact]
    public void ByPlayer_groups_same_type_and_orders_by_sort()
    {
        var holdings = new[]
        {
            new JewelHolding("bob", Necklace.Id),
            new JewelHolding("bob", Ring.Id),
            new JewelHolding("bob", Ring.Id),
        };

        var result = JewelCalculator.ByPlayer(Types, holdings).Of("bob");

        Assert.Equal([new PlayerJewel(Ring, 2), new PlayerJewel(Necklace, 1)], result);
    }

    [Fact]
    public void ByPlayer_unknown_player_is_empty()
    {
        var byPlayer = JewelCalculator.ByPlayer(Types, [new JewelHolding("bob", Ring.Id)]);

        Assert.Empty(byPlayer.Of("nobody"));
        Assert.Empty(JewelsByPlayer.Empty.Of("bob"));
    }

    [Fact]
    public void ByPlayer_ignores_unknown_type()
    {
        var byPlayer = JewelCalculator.ByPlayer(Types, [new JewelHolding("bob", 99), new JewelHolding("bob", Ring.Id)]);

        Assert.Equal([new PlayerJewel(Ring, 1)], byPlayer.Of("bob"));
    }

    [Fact]
    public void Gallery_counts_pieces_and_lists_holders()
    {
        var holdings = new[]
        {
            new JewelHolding("bob", Ring.Id),
            new JewelHolding("bob", Ring.Id),
            new JewelHolding("alice", Ring.Id),
            new JewelHolding("carl", Earring.Id),
        };

        var gallery = JewelCalculator.Gallery(Types, holdings, [Bob, Alice, Carl]);

        Assert.Equal([Ring, Earring, Necklace], gallery.Select(s => s.Type));

        Assert.Equal(3, gallery[0].Count);
        Assert.Equal([new JewelHolder(Alice, 1), new JewelHolder(Bob, 2)], gallery[0].Holders);

        Assert.Equal(1, gallery[1].Count);
        var carl = Assert.Single(gallery[1].Holders);
        Assert.Equal("carl", carl.Player.UserId);
        Assert.False(carl.Player.IsActive);

        Assert.Equal(0, gallery[2].Count);
        Assert.Empty(gallery[2].Holders);
    }

    [Fact]
    public void Gallery_ignores_holdings_of_unknown_players()
    {
        var gallery = JewelCalculator.Gallery(Types, [new JewelHolding("ghost", Ring.Id)], [Bob]);

        Assert.All(gallery, s => Assert.Equal(0, s.Count));
        Assert.All(gallery, s => Assert.Empty(s.Holders));
    }
}
