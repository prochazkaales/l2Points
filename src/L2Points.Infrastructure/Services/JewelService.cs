using L2Points.Domain.Jewels;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>Jewel gallery and admin assignment of jewel pieces to players.</summary>
public sealed class JewelService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Piece counts and holders of every jewel type; holders include inactive players.</summary>
    public async Task<IReadOnlyList<JewelTypeSummary>> GetGalleryAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var types = await db.LoadJewelTypesAsync(ct);
        var holdings = await db.LoadJewelHoldingsAsync(ct);
        var players = await LoadAllPlayersAsync(db, ct);
        return JewelCalculator.Gallery(types, holdings, players);
    }

    /// <summary>All players (including inactive), all jewel types and the jewels each player holds.</summary>
    public async Task<JewelAssignments> GetAssignmentsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var players = await LoadAllPlayersAsync(db, ct);
        var types = await db.LoadJewelTypesAsync(ct);
        var holdings = await db.LoadJewelHoldingsAsync(ct);
        return new JewelAssignments(players, types, JewelCalculator.ByPlayer(types, holdings));
    }

    /// <summary>Gives the player one more piece of the jewel type (a player may hold several pieces of the same type).</summary>
    public async Task<JewelChangeResult> AssignAsync(string userId, int jewelTypeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var check = await CheckAsync(db, userId, jewelTypeId, ct);
        if (check != JewelChangeResult.Ok)
            return check;

        db.UserJewels.Add(new UserJewel { UserId = userId, JewelTypeId = jewelTypeId });
        return await SaveAsync(db, userId, ct);
    }

    /// <summary>Removes a single piece (the one with the lowest id) of the jewel type from the player.</summary>
    public async Task<JewelChangeResult> RemoveOneAsync(string userId, int jewelTypeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var check = await CheckAsync(db, userId, jewelTypeId, ct);
        if (check != JewelChangeResult.Ok)
            return check;

        var piece = await db.UserJewels
            .Where(j => j.UserId == userId && j.JewelTypeId == jewelTypeId)
            .OrderBy(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (piece is null)
            return JewelChangeResult.NotHeld;

        db.UserJewels.Remove(piece);
        return await SaveAsync(db, userId, ct);
    }

    /// <summary>The player may be deleted by another admin between the check and the save.</summary>
    private async Task<JewelChangeResult> SaveAsync(AppDbContext db, string userId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return JewelChangeResult.Ok;
        }
        catch (DbUpdateException)
        {
            await using var fresh = await dbFactory.CreateDbContextAsync(ct);
            if (!await fresh.Users.AnyAsync(u => u.Id == userId, ct))
                return JewelChangeResult.UnknownPlayer;
            throw;
        }
    }

    private static async Task<JewelChangeResult> CheckAsync(AppDbContext db, string userId, int jewelTypeId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            return JewelChangeResult.UnknownPlayer;
        // Hidden readers are not part of the stats, so their jewels cannot be edited.
        if (await db.IsHiddenFromStatsAsync(userId, ct))
            return JewelChangeResult.HiddenPlayer;
        if (!await db.JewelTypes.AnyAsync(t => t.Id == jewelTypeId, ct))
            return JewelChangeResult.UnknownJewelType;
        return JewelChangeResult.Ok;
    }

    private static async Task<List<PlayerInfo>> LoadAllPlayersAsync(AppDbContext db, CancellationToken ct)
    {
        var players = await db.Users.AsNoTracking().Where(u => u.ShowInStats).SelectPlayerInfo().ToListAsync(ct);
        return players
            .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.UserId, StringComparer.Ordinal)
            .ToList();
    }
}
