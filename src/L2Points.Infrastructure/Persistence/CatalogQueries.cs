using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Persistence;

public static class CatalogQueries
{
    /// <summary>Storage format of the tracking start date in AppSettings.</summary>
    public const string TrackingStartFormat = "yyyy-MM-dd";

    /// <summary>Whole catalog including inactive activities, ordered by SortOrder.</summary>
    public static async Task<IReadOnlyList<Activity>> LoadActivitiesAsync(this AppDbContext db, CancellationToken ct = default) =>
        await db.Activities
            .AsNoTracking()
            .Include(a => a.Spawns)
            .Include(a => a.SlotPoints)
            .AsSplitQuery()
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    /// <summary>Global start of tracking; falls back to the default when the setting is missing.</summary>
    public static async Task<DateOnly> GetTrackingStartAsync(this AppDbContext db, CancellationToken ct = default)
    {
        var value = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == AppSettingKeys.TrackingStartDate)
            .Select(s => s.Value)
            .SingleOrDefaultAsync(ct);

        return DateOnly.ParseExact(value ?? DefaultCatalog.TrackingStartDate, TrackingStartFormat, CultureInfo.InvariantCulture);
    }
}
