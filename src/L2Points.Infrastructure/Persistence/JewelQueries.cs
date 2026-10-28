using L2Points.Domain.Jewels;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Persistence;

public static class JewelQueries
{
    /// <summary>All jewel types ordered by SortOrder.</summary>
    public static async Task<IReadOnlyList<JewelType>> LoadJewelTypesAsync(this AppDbContext db, CancellationToken ct = default) =>
        await db.JewelTypes
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

    /// <summary>Every owned jewel piece of every player.</summary>
    public static Task<List<JewelHolding>> LoadJewelHoldingsAsync(this AppDbContext db, CancellationToken ct = default) =>
        db.UserJewels
            .AsNoTracking()
            .Select(j => new JewelHolding(j.UserId, j.JewelTypeId))
            .ToListAsync(ct);

    /// <summary>Jewels of all players grouped by type.</summary>
    public static async Task<JewelsByPlayer> LoadJewelsByPlayerAsync(this AppDbContext db, CancellationToken ct = default)
    {
        var types = await db.LoadJewelTypesAsync(ct);
        var holdings = await db.LoadJewelHoldingsAsync(ct);
        return JewelCalculator.ByPlayer(types, holdings);
    }
}
