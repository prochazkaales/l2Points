using L2Points.Domain.Activities;
using L2Points.Domain.Jewels;
using L2Points.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Persistence;

/// <summary>
/// Seeds the default catalog at runtime. Only adds what is missing (activities and jewel types by Code, settings by Key)
/// and never overwrites values an admin has changed.
/// </summary>
public class ReferenceDataSeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingCodes = (await db.Activities.Select(a => a.Code).ToListAsync(ct)).ToHashSet();
        foreach (var activity in DefaultCatalog.CreateActivities().Where(a => !existingCodes.Contains(a.Code)))
        {
            db.Activities.Add(activity);
        }

        var existingJewelCodes = (await db.JewelTypes.Select(t => t.Code).ToListAsync(ct)).ToHashSet();
        foreach (var jewelType in DefaultJewels.CreateJewelTypes().Where(t => !existingJewelCodes.Contains(t.Code)))
        {
            db.JewelTypes.Add(jewelType);
        }

        if (!await db.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.TrackingStartDate, ct))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.TrackingStartDate,
                Value = DefaultCatalog.TrackingStartDate,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
