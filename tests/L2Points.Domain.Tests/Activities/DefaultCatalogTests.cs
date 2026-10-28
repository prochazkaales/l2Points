using L2Points.Domain.Activities;
using L2Points.Domain.Settings;

namespace L2Points.Domain.Tests.Activities;

public class DefaultCatalogTests
{
    [Fact]
    public void Catalog_has_14_activities_with_unique_codes()
    {
        var activities = DefaultCatalog.CreateActivities();

        Assert.Equal(14, activities.Count);
        Assert.Equal(14, activities.Select(a => a.Code).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 14), activities.Select(a => a.SortOrder));
        Assert.All(activities, a => Assert.Equal(0, a.Id));
        Assert.All(activities, a => Assert.Equal(1, a.Points));
        Assert.All(activities, a => Assert.True(a.IsActive));
    }

    [Fact]
    public void Catalog_returns_new_instances_each_call()
    {
        Assert.NotSame(DefaultCatalog.CreateActivities()[0], DefaultCatalog.CreateActivities()[0]);
    }

    [Fact]
    public void Catalog_has_40_chaotic_spawns()
    {
        var chaotic = DefaultCatalog.CreateActivities().Where(a => a.Category == ActivityCategory.ChaoticEpic);

        Assert.Equal(40, chaotic.Sum(a => a.Spawns.Count));
    }

    [Fact]
    public void Catalog_flexible_expected_counts()
    {
        var expected = new Dictionary<string, int>
        {
            ["QueenAnt"] = 6, ["Core"] = 4, ["Orfen"] = 4, ["Zaken"] = 4, ["Frintezza"] = 3,
        };

        var flexible = DefaultCatalog.CreateActivities().Where(a => a.ScheduleType == ScheduleType.Flexible).ToList();

        Assert.Equal(5, flexible.Count);
        foreach (var activity in flexible)
            Assert.Equal(expected[activity.Code], activity.ExpectedPerWeek);
    }

    [Fact]
    public void Catalog_has_expected_names_and_categories()
    {
        var byCode = DefaultCatalog.CreateActivities().ToDictionary(a => a.Code);

        Assert.Equal("Queen Ant", byCode["QueenAnt"].Name);
        Assert.Equal("Chaotic Queen Ant", byCode["ChaoticQueenAnt"].Name);
        Assert.Equal("Castle Siege", byCode["CastleSiege"].Name);
        Assert.Equal(ActivityCategory.CastleSiege, byCode["CastleSiege"].Category);
        Assert.Equal(ActivityCategory.NormalEpic, byCode["Baium"].Category);
        Assert.Equal(ActivityCategory.ChaoticEpic, byCode["ChaoticFrintezza"].Category);
    }

    [Fact]
    public void Catalog_fixed_schedule_values()
    {
        var byCode = DefaultCatalog.CreateActivities().ToDictionary(a => a.Code);

        Assert.Equal(ScheduleType.Weekly, byCode["Baium"].ScheduleType);
        Assert.Equal(DayOfWeek.Saturday, byCode["Baium"].WeeklyDay);
        Assert.Equal(new TimeOnly(20, 0), byCode["Baium"].TimeUtc);
        Assert.Equal(ScheduleType.Biweekly, byCode["Antharas"].ScheduleType);
        Assert.Equal(DayOfWeek.Sunday, byCode["Antharas"].WeeklyDay);
        Assert.Equal(new DateOnly(2026, 11, 1), byCode["Antharas"].BiweeklyAnchorDate);
        Assert.Equal(new DateOnly(2026, 11, 8), byCode["Valakas"].BiweeklyAnchorDate);
        Assert.Equal(ScheduleType.Weekly, byCode["CastleSiege"].ScheduleType);
        Assert.Equal(DayOfWeek.Sunday, byCode["CastleSiege"].WeeklyDay);
        Assert.Equal(new TimeOnly(18, 0), byCode["CastleSiege"].TimeUtc);
    }

    [Fact]
    public void Catalog_chaotic_spawn_times()
    {
        var activities = DefaultCatalog.CreateActivities();
        var zaken = activities.Single(a => a.Code == "ChaoticZaken");
        var frintezza = activities.Single(a => a.Code == "ChaoticFrintezza");

        Assert.Equal(6, zaken.Spawns.Count);
        Assert.Equal(
            new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            zaken.Spawns.Select(s => s.DayOfWeek).Distinct());
        Assert.Contains(zaken.Spawns, s => s.DayOfWeek == DayOfWeek.Monday && s.Slot == EventSlot.Evening && s.TimeUtc == new TimeOnly(19, 30));
        Assert.Contains(zaken.Spawns, s => s.DayOfWeek == DayOfWeek.Monday && s.Slot == EventSlot.Morning && s.TimeUtc == new TimeOnly(3, 0));
        Assert.Equal(4, frintezza.Spawns.Count);
        Assert.Equal(
            new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday },
            frintezza.Spawns.Select(s => s.DayOfWeek).Distinct());
    }

    [Fact]
    public void Tracking_start_date_constant()
    {
        Assert.Equal("2026-10-26", DefaultCatalog.TrackingStartDate);
        Assert.Equal("TrackingStartDate", AppSettingKeys.TrackingStartDate);
    }
}
