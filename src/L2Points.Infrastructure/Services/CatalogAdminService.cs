using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Settings;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>Admin edits of the activity catalog (points, expected counts, schedule) and the global tracking start.</summary>
public sealed class CatalogAdminService(IDbContextFactory<AppDbContext> dbFactory)
{
    private const string AntharasCode = "Antharas";

    private const string ValakasCode = "Valakas";

    /// <summary>Whole catalog including inactive activities.</summary>
    public async Task<IReadOnlyList<Activity>> GetActivitiesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LoadActivitiesAsync(ct);
    }

    public async Task<DateOnly> GetTrackingStartAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.GetTrackingStartAsync(ct);
    }

    /// <summary>Writes points, expected count, active flag and slot point overrides of one activity.</summary>
    public async Task<CatalogChangeResult> UpdateActivityAsync(int activityId, ActivitySettings settings, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var activity = await LoadTrackedAsync(db, activityId, ct);
        if (activity is null)
            return CatalogChangeResult.UnknownActivity;

        var check = CatalogRules.ValidateSettings(activity, settings);
        if (check != CatalogChangeResult.Ok)
            return check;

        activity.Points = settings.Points;
        activity.ExpectedPerWeek = settings.ExpectedPerWeek;
        activity.IsActive = settings.IsActive;
        ApplySlotPoints(db, activity, EventSlot.Evening, settings.EveningPoints);
        ApplySlotPoints(db, activity, EventSlot.Morning, settings.MorningPoints);

        await db.SaveChangesAsync(ct);
        return CatalogChangeResult.Ok;
    }

    /// <summary>
    /// Replaces the spawn set of a slotted activity as a diff (update, remove, add) in one save,
    /// so the unique index (activity, day, slot) is never violated.
    /// </summary>
    public async Task<CatalogChangeResult> SaveSpawnsAsync(int activityId, IReadOnlyList<SpawnInput> spawns, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var activity = await LoadTrackedAsync(db, activityId, ct);
        if (activity is null)
            return CatalogChangeResult.UnknownActivity;

        var check = CatalogRules.ValidateSpawns(activity, spawns);
        if (check != CatalogChangeResult.Ok)
            return check;

        var requested = spawns.ToDictionary(s => (s.Day, s.Slot));
        foreach (var existing in activity.Spawns.ToList())
        {
            if (requested.Remove((existing.DayOfWeek, existing.Slot), out var input))
            {
                existing.TimeUtc = input.TimeUtc;
            }
            else
            {
                db.ActivitySpawns.Remove(existing);
            }
        }

        foreach (var input in requested.Values)
        {
            activity.Spawns.Add(new ActivitySpawn { DayOfWeek = input.Day, Slot = input.Slot, TimeUtc = input.TimeUtc });
        }

        await db.SaveChangesAsync(ct);
        return CatalogChangeResult.Ok;
    }

    /// <summary>Sets the Antharas Sunday anchor; Valakas is always one week later.</summary>
    public async Task<CatalogChangeResult> SetAntharasValakasAnchorAsync(DateOnly antharasSunday, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var antharas = await db.Activities.SingleOrDefaultAsync(a => a.Code == AntharasCode, ct);
        var valakas = await db.Activities.SingleOrDefaultAsync(a => a.Code == ValakasCode, ct);
        if (antharas is null || valakas is null)
            return CatalogChangeResult.UnknownActivity;

        var check = CatalogRules.ValidateBiweeklyAnchor(antharas, antharasSunday);
        if (check != CatalogChangeResult.Ok)
            return check;

        antharas.BiweeklyAnchorDate = antharasSunday;
        valakas.BiweeklyAnchorDate = antharasSunday.AddDays(7);

        await db.SaveChangesAsync(ct);
        return CatalogChangeResult.Ok;
    }

    /// <summary>Sets the global start of tracking; applies retroactively to all statistics.</summary>
    public async Task<CatalogChangeResult> SetTrackingStartAsync(DateOnly date, CancellationToken ct = default)
    {
        if (!CatalogRules.IsValidTrackingStart(date))
            return CatalogChangeResult.InvalidValue;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var value = date.ToString(CatalogQueries.TrackingStartFormat, CultureInfo.InvariantCulture);
        var setting = await db.AppSettings.SingleOrDefaultAsync(s => s.Key == AppSettingKeys.TrackingStartDate, ct);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = AppSettingKeys.TrackingStartDate, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync(ct);
        return CatalogChangeResult.Ok;
    }

    private static Task<Activity?> LoadTrackedAsync(AppDbContext db, int activityId, CancellationToken ct) =>
        db.Activities
            .Include(a => a.Spawns)
            .Include(a => a.SlotPoints)
            .AsSplitQuery()
            .SingleOrDefaultAsync(a => a.Id == activityId, ct);

    /// <summary>Upserts the override row, or removes it when <paramref name="points"/> is null.</summary>
    private static void ApplySlotPoints(AppDbContext db, Activity activity, EventSlot slot, int? points)
    {
        var existing = activity.SlotPoints.SingleOrDefault(s => s.Slot == slot);
        if (points is null)
        {
            if (existing is not null)
                db.ActivitySlotPoints.Remove(existing);
        }
        else if (existing is null)
        {
            activity.SlotPoints.Add(new ActivitySlotPoints { Slot = slot, Points = points.Value });
        }
        else
        {
            existing.Points = points.Value;
        }
    }
}
