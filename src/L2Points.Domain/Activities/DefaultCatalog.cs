namespace L2Points.Domain.Activities;

/// <summary>Initial activity catalog used to seed the database.</summary>
public static class DefaultCatalog
{
    public const string TrackingStartDate = "2026-10-26";

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>Creates fresh, unsaved instances (Id = 0) on every call.</summary>
    public static IReadOnlyList<Activity> CreateActivities()
    {
        var sort = 0;
        return
        [
            Flexible("QueenAnt", "Queen Ant", 6, ++sort),
            Flexible("Core", "Core", 4, ++sort),
            Flexible("Orfen", "Orfen", 4, ++sort),
            Flexible("Zaken", "Zaken", 4, ++sort),
            Flexible("Frintezza", "Frintezza", 3, ++sort),
            Fixed("Baium", "Baium", ActivityCategory.NormalEpic, ScheduleType.Weekly, DayOfWeek.Saturday, new TimeOnly(20, 0), null, ++sort),
            Fixed("Antharas", "Antharas", ActivityCategory.NormalEpic, ScheduleType.Biweekly, DayOfWeek.Sunday, null, new DateOnly(2026, 11, 1), ++sort),
            Fixed("Valakas", "Valakas", ActivityCategory.NormalEpic, ScheduleType.Biweekly, DayOfWeek.Sunday, null, new DateOnly(2026, 11, 8), ++sort),
            Chaotic("ChaoticQueenAnt", "Chaotic Queen Ant", Weekdays, new TimeOnly(18, 0), new TimeOnly(1, 30), ++sort),
            Chaotic("ChaoticCore", "Chaotic Core", Weekdays, new TimeOnly(18, 30), new TimeOnly(2, 0), ++sort),
            Chaotic("ChaoticOrfen", "Chaotic Orfen", Weekdays, new TimeOnly(19, 0), new TimeOnly(2, 30), ++sort),
            Chaotic("ChaoticZaken", "Chaotic Zaken", [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], new TimeOnly(19, 30), new TimeOnly(3, 0), ++sort),
            Chaotic("ChaoticFrintezza", "Chaotic Frintezza", [DayOfWeek.Tuesday, DayOfWeek.Thursday], new TimeOnly(19, 30), new TimeOnly(3, 0), ++sort),
            Fixed("CastleSiege", "Castle Siege", ActivityCategory.CastleSiege, ScheduleType.Weekly, DayOfWeek.Sunday, new TimeOnly(18, 0), null, ++sort),
        ];
    }

    private static Activity Flexible(string code, string name, int expectedPerWeek, int sortOrder) => new()
    {
        Code = code,
        Name = name,
        Category = ActivityCategory.NormalEpic,
        ScheduleType = ScheduleType.Flexible,
        Points = 1,
        ExpectedPerWeek = expectedPerWeek,
        SortOrder = sortOrder,
    };

    private static Activity Fixed(
        string code, string name, ActivityCategory category, ScheduleType type,
        DayOfWeek day, TimeOnly? time, DateOnly? anchor, int sortOrder) => new()
    {
        Code = code,
        Name = name,
        Category = category,
        ScheduleType = type,
        Points = 1,
        WeeklyDay = day,
        TimeUtc = time,
        BiweeklyAnchorDate = anchor,
        SortOrder = sortOrder,
    };

    private static Activity Chaotic(
        string code, string name, DayOfWeek[] days, TimeOnly evening, TimeOnly morning, int sortOrder)
    {
        var activity = new Activity
        {
            Code = code,
            Name = name,
            Category = ActivityCategory.ChaoticEpic,
            ScheduleType = ScheduleType.Slotted,
            Points = 1,
            SortOrder = sortOrder,
        };

        foreach (var day in days)
        {
            activity.Spawns.Add(new ActivitySpawn { DayOfWeek = day, Slot = EventSlot.Evening, TimeUtc = evening });
            activity.Spawns.Add(new ActivitySpawn { DayOfWeek = day, Slot = EventSlot.Morning, TimeUtc = morning });
        }

        return activity;
    }
}
