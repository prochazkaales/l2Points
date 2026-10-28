namespace L2Points.Domain.Activities;

/// <summary>Validation rules for admin edits of the activity catalog. Pure functions, no I/O.</summary>
public static class CatalogRules
{
    public const int MaxPoints = 1000;

    public const int MinExpectedPerWeek = 1;

    /// <summary>A flexible boss can be ticked at most once a day.</summary>
    public const int MaxExpectedPerWeek = 7;

    /// <summary>
    /// Spawns must be before this UTC time so the occurrence date (UTC date of the spawn)
    /// equals the Europe/Prague date (summer time is UTC+2).
    /// </summary>
    public static readonly TimeOnly SpawnTimeLimitUtc = new(22, 0);

    private const int MinYear = 2000;

    private const int MaxYear = 2100;

    public static CatalogChangeResult ValidateSettings(Activity activity, ActivitySettings settings)
    {
        if (!IsPointsValue(settings.Points))
        {
            return CatalogChangeResult.InvalidValue;
        }

        if (activity.ScheduleType == ScheduleType.Slotted)
        {
            if (!IsNullOrPointsValue(settings.EveningPoints) || !IsNullOrPointsValue(settings.MorningPoints))
            {
                return CatalogChangeResult.InvalidValue;
            }
        }
        else if (settings.EveningPoints is not null || settings.MorningPoints is not null)
        {
            return CatalogChangeResult.InvalidValue;
        }

        if (activity.ScheduleType == ScheduleType.Flexible)
        {
            if (settings.ExpectedPerWeek is not { } expected
                || expected < MinExpectedPerWeek || expected > MaxExpectedPerWeek)
            {
                return CatalogChangeResult.InvalidValue;
            }
        }
        else if (settings.ExpectedPerWeek is not null)
        {
            return CatalogChangeResult.InvalidValue;
        }

        return CatalogChangeResult.Ok;
    }

    public static CatalogChangeResult ValidateSpawns(Activity activity, IReadOnlyList<SpawnInput> spawns)
    {
        if (activity.ScheduleType != ScheduleType.Slotted)
        {
            return CatalogChangeResult.InvalidValue;
        }

        var seen = new HashSet<(DayOfWeek, EventSlot)>();
        foreach (var spawn in spawns)
        {
            if (spawn.Slot == EventSlot.None)
            {
                return CatalogChangeResult.InvalidValue;
            }

            if (spawn.TimeUtc >= SpawnTimeLimitUtc)
            {
                return CatalogChangeResult.InvalidSpawnTime;
            }

            if (!seen.Add((spawn.Day, spawn.Slot)))
            {
                return CatalogChangeResult.DuplicateSpawn;
            }
        }

        return CatalogChangeResult.Ok;
    }

    public static CatalogChangeResult ValidateBiweeklyAnchor(Activity activity, DateOnly anchor)
    {
        if (activity.ScheduleType != ScheduleType.Biweekly || !IsYearInRange(anchor))
        {
            return CatalogChangeResult.InvalidValue;
        }

        return anchor.DayOfWeek == activity.WeeklyDay
            ? CatalogChangeResult.Ok
            : CatalogChangeResult.AnchorNotOnWeeklyDay;
    }

    public static bool IsValidTrackingStart(DateOnly date) => IsYearInRange(date);

    private static bool IsPointsValue(int value) => value is >= 0 and <= MaxPoints;

    private static bool IsNullOrPointsValue(int? value) => value is null || IsPointsValue(value.Value);

    private static bool IsYearInRange(DateOnly date) => date.Year is >= MinYear and <= MaxYear;
}
