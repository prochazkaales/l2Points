using L2Points.Domain.Activities;

namespace L2Points.Domain.Tests.Activities;

public class CatalogRulesTests
{
    private static Activity Get(string code) =>
        DefaultCatalog.CreateActivities().Single(a => a.Code == code);

    private static ActivitySettings Settings(
        int points = 1, int? evening = null, int? morning = null, int? expected = null, bool active = true) =>
        new(points, evening, morning, expected, active);

    [Fact]
    public void Settings_valid_for_each_schedule_type()
    {
        Assert.Equal(CatalogChangeResult.Ok,
            CatalogRules.ValidateSettings(Get("QueenAnt"), new ActivitySettings(5, null, null, 6, true)));
        Assert.Equal(CatalogChangeResult.Ok,
            CatalogRules.ValidateSettings(Get("ChaoticCore"), new ActivitySettings(2, 3, null, null, false)));
        Assert.Equal(CatalogChangeResult.Ok,
            CatalogRules.ValidateSettings(Get("Baium"), new ActivitySettings(0, null, null, null, true)));
    }

    [Theory]
    [InlineData(-1, CatalogChangeResult.InvalidValue)]
    [InlineData(1001, CatalogChangeResult.InvalidValue)]
    [InlineData(1000, CatalogChangeResult.Ok)]
    public void Settings_points_out_of_range(int points, CatalogChangeResult expected)
    {
        Assert.Equal(expected, CatalogRules.ValidateSettings(Get("Baium"), Settings(points: points)));
    }

    [Fact]
    public void Settings_slot_points_only_for_slotted()
    {
        Assert.Equal(CatalogChangeResult.InvalidValue,
            CatalogRules.ValidateSettings(Get("Baium"), Settings(evening: 2)));
        Assert.Equal(CatalogChangeResult.InvalidValue,
            CatalogRules.ValidateSettings(Get("ChaoticCore"), Settings(morning: 1001)));
    }

    [Theory]
    [InlineData(null, CatalogChangeResult.InvalidValue)]
    [InlineData(0, CatalogChangeResult.InvalidValue)]
    [InlineData(8, CatalogChangeResult.InvalidValue)]
    [InlineData(7, CatalogChangeResult.Ok)]
    public void Settings_expected_per_week_rules(int? expected, CatalogChangeResult result)
    {
        Assert.Equal(result, CatalogRules.ValidateSettings(Get("QueenAnt"), Settings(expected: expected)));
    }

    [Fact]
    public void Settings_expected_per_week_rejected_for_non_flexible()
    {
        Assert.Equal(CatalogChangeResult.InvalidValue,
            CatalogRules.ValidateSettings(Get("Baium"), Settings(expected: 1)));
    }

    [Fact]
    public void Spawns_valid_set_and_empty_set()
    {
        var zaken = Get("ChaoticZaken");

        Assert.Equal(CatalogChangeResult.Ok, CatalogRules.ValidateSpawns(zaken,
        [
            new SpawnInput(DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(19, 30)),
            new SpawnInput(DayOfWeek.Monday, EventSlot.Morning, new TimeOnly(3, 0)),
        ]));
        Assert.Equal(CatalogChangeResult.Ok, CatalogRules.ValidateSpawns(zaken, []));
    }

    [Theory]
    [InlineData(21, 59, CatalogChangeResult.Ok)]
    [InlineData(22, 0, CatalogChangeResult.InvalidSpawnTime)]
    [InlineData(23, 30, CatalogChangeResult.InvalidSpawnTime)]
    public void Spawns_time_limit(int hour, int minute, CatalogChangeResult expected)
    {
        var result = CatalogRules.ValidateSpawns(Get("ChaoticZaken"),
            [new SpawnInput(DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(hour, minute))]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Spawns_duplicate_day_and_slot()
    {
        var result = CatalogRules.ValidateSpawns(Get("ChaoticZaken"),
        [
            new SpawnInput(DayOfWeek.Tuesday, EventSlot.Evening, new TimeOnly(19, 0)),
            new SpawnInput(DayOfWeek.Tuesday, EventSlot.Evening, new TimeOnly(20, 0)),
        ]);

        Assert.Equal(CatalogChangeResult.DuplicateSpawn, result);
    }

    [Fact]
    public void Spawns_reject_none_slot_and_non_slotted_activity()
    {
        Assert.Equal(CatalogChangeResult.InvalidValue, CatalogRules.ValidateSpawns(Get("ChaoticZaken"),
            [new SpawnInput(DayOfWeek.Monday, EventSlot.None, new TimeOnly(19, 0))]));
        Assert.Equal(CatalogChangeResult.InvalidValue, CatalogRules.ValidateSpawns(Get("Baium"),
            [new SpawnInput(DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(19, 0))]));
    }

    [Fact]
    public void Anchor_must_be_on_weekly_day()
    {
        var antharas = Get("Antharas");

        Assert.Equal(CatalogChangeResult.Ok,
            CatalogRules.ValidateBiweeklyAnchor(antharas, new DateOnly(2026, 11, 15)));
        Assert.Equal(CatalogChangeResult.AnchorNotOnWeeklyDay,
            CatalogRules.ValidateBiweeklyAnchor(antharas, new DateOnly(2026, 11, 14)));
        Assert.Equal(CatalogChangeResult.InvalidValue,
            CatalogRules.ValidateBiweeklyAnchor(Get("Baium"), new DateOnly(2026, 11, 14)));
    }

    [Theory]
    [InlineData(2026, 10, 26, true)]
    [InlineData(1999, 12, 31, false)]
    [InlineData(2101, 1, 1, false)]
    public void Tracking_start_year_range(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, CatalogRules.IsValidTrackingStart(new DateOnly(year, month, day)));
    }
}
