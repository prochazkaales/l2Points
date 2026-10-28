using L2Points.Domain.Jewels;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class JewelServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private sealed record Arranged(ServiceProvider Provider, JewelService Service, string Bob, string Alice, string Carl) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();

        public async Task<int> TypeIdAsync(string code)
        {
            await using var db = Provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            return await db.JewelTypes.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
        }

        public async Task<int> PieceCountAsync()
        {
            await using var db = Provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            return await db.UserJewels.CountAsync();
        }
    }

    private static async Task<Arranged> ArrangeAsync()
    {
        var provider = TestServices.Create();
        await TestData.SeedCatalogAsync(provider);
        var bob = await TestData.CreatePlayerAsync(provider, "bob", Monday);
        var alice = await TestData.CreatePlayerAsync(provider, "Alice", Monday);
        var carl = await TestData.CreatePlayerAsync(provider, "carl", Monday, deactivatedAt: Monday);
        var service = new JewelService(provider.GetRequiredService<IDbContextFactory<AppDbContext>>());
        return new Arranged(provider, service, bob.Id, alice.Id, carl.Id);
    }

    [Fact]
    public async Task Assign_adds_piece_and_allows_duplicates()
    {
        await using var x = await ArrangeAsync();
        var baium = await x.TypeIdAsync("BaiumRing");

        Assert.Equal(JewelChangeResult.Ok, await x.Service.AssignAsync(x.Bob, baium));
        Assert.Equal(JewelChangeResult.Ok, await x.Service.AssignAsync(x.Bob, baium));

        var jewels = (await x.Service.GetAssignmentsAsync()).Jewels.Of(x.Bob);
        var piece = Assert.Single(jewels);
        Assert.Equal("BaiumRing", piece.Type.Code);
        Assert.Equal(2, piece.Count);
    }

    [Fact]
    public async Task Assign_unknown_player_or_type_is_rejected()
    {
        await using var x = await ArrangeAsync();
        var baium = await x.TypeIdAsync("BaiumRing");

        Assert.Equal(JewelChangeResult.UnknownPlayer, await x.Service.AssignAsync("nope", baium));
        Assert.Equal(JewelChangeResult.UnknownJewelType, await x.Service.AssignAsync(x.Bob, 999));
        Assert.Equal(0, await x.PieceCountAsync());
    }

    [Fact]
    public async Task Remove_one_removes_single_piece()
    {
        await using var x = await ArrangeAsync();
        var baium = await x.TypeIdAsync("BaiumRing");
        await x.Service.AssignAsync(x.Bob, baium);
        await x.Service.AssignAsync(x.Bob, baium);

        Assert.Equal(JewelChangeResult.Ok, await x.Service.RemoveOneAsync(x.Bob, baium));

        var piece = Assert.Single((await x.Service.GetAssignmentsAsync()).Jewels.Of(x.Bob));
        Assert.Equal(1, piece.Count);
    }

    [Fact]
    public async Task Remove_when_not_held_returns_NotHeld()
    {
        await using var x = await ArrangeAsync();
        var baium = await x.TypeIdAsync("BaiumRing");

        Assert.Equal(JewelChangeResult.NotHeld, await x.Service.RemoveOneAsync(x.Bob, baium));
        Assert.Equal(JewelChangeResult.UnknownPlayer, await x.Service.RemoveOneAsync("nope", baium));
        Assert.Equal(JewelChangeResult.UnknownJewelType, await x.Service.RemoveOneAsync(x.Bob, 999));
    }

    [Fact]
    public async Task Gallery_counts_pieces_including_inactive_holders()
    {
        await using var x = await ArrangeAsync();
        var queenAnt = await x.TypeIdAsync("QueenAntRing");
        await x.Service.AssignAsync(x.Bob, queenAnt);
        await x.Service.AssignAsync(x.Bob, queenAnt);
        await x.Service.AssignAsync(x.Carl, queenAnt);

        var gallery = await x.Service.GetGalleryAsync();

        Assert.Equal(
            ["QueenAntRing", "CoreRing", "OrfenEarring", "ZakenEarring", "FrintezzaNecklace", "BaiumRing", "AntharasEarring", "ValakasNecklace"],
            gallery.Select(s => s.Type.Code));
        var summary = gallery[0];
        Assert.Equal(3, summary.Count);
        Assert.Equal([("bob", 2), ("carl", 1)], summary.Holders.Select(h => (h.Player.Nick, h.Count)));
        Assert.False(summary.Holders[1].Player.IsActive);
        Assert.All(gallery.Skip(1), s => Assert.Empty(s.Holders));
    }

    [Fact]
    public async Task Assignments_list_all_players_ordered_by_nick()
    {
        await using var x = await ArrangeAsync();

        var assignments = await x.Service.GetAssignmentsAsync();

        Assert.Equal(["Alice", "bob", "carl"], assignments.Players.Select(p => p.Nick));
        Assert.Equal(8, assignments.Types.Count);
    }
}
