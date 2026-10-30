using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Web.Formatting;

namespace L2Points.Web.Tests.Formatting;

public class DisplayFormatTests
{
    [Theory]
    [InlineData(44.5, "45 %")]
    [InlineData(44.4, "44 %")]
    [InlineData(0.0, "0 %")]
    [InlineData(100.0, "100 %")]
    public void Percent_rounds_half_away_from_zero(double value, string expected) =>
        Assert.Equal(expected, DisplayFormat.Percent(value));

    [Theory]
    [InlineData(StatCategory.Normal, "normal")]
    [InlineData(StatCategory.Evening, "evening")]
    [InlineData(StatCategory.Morning, "morning")]
    [InlineData(StatCategory.CastleSiege, "cs")]
    public void StatCategoryKey_matches_the_event_kind_keys(StatCategory category, string expected) =>
        Assert.Equal(expected, DisplayFormat.StatCategoryKey(category));

    [Fact]
    public void Percent_null_is_dash() => Assert.Equal("–", DisplayFormat.Percent(null));

    [Theory]
    [InlineData(100.0, "high")]
    [InlineData(79.5, "high")]
    [InlineData(79.4, "mid")]
    [InlineData(50.0, "mid")]
    [InlineData(49.4, "low")]
    [InlineData(0.5, "low")]
    [InlineData(0.4, "zero")]
    [InlineData(0.0, "zero")]
    public void PercentTone_follows_the_shown_number(double value, string expected) =>
        Assert.Equal(expected, DisplayFormat.PercentTone(value));

    [Fact]
    public void PercentTone_null_when_missing() => Assert.Null(DisplayFormat.PercentTone(null));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(950, "950")]
    [InlineData(1420, "1 420")]
    public void Points_group_thousands(int value, string expected) =>
        Assert.Equal(expected, DisplayFormat.Points(value));

    [Theory]
    [InlineData(1, "1 bod")]
    [InlineData(3, "3 body")]
    [InlineData(0, "0 bodů")]
    [InlineData(135, "135 bodů")]
    public void PointsCount_czech(int count, string expected) =>
        Assert.Equal(expected, DisplayFormat.PointsCount(count));

    [Theory]
    [InlineData(ActivityCategory.NormalEpic, EventSlot.None, "Normal", "normal")]
    [InlineData(ActivityCategory.ChaoticEpic, EventSlot.Evening, "Večerní", "evening")]
    [InlineData(ActivityCategory.ChaoticEpic, EventSlot.Morning, "Ranní", "morning")]
    [InlineData(ActivityCategory.CastleSiege, EventSlot.None, "CS", "cs")]
    public void EventKind_label_and_key(ActivityCategory category, EventSlot slot, string label, string key)
    {
        Assert.Equal(label, DisplayFormat.EventKindLabel(category, slot));
        Assert.Equal(key, DisplayFormat.EventKindKey(category, slot));
    }

    [Fact]
    public void EventTime_in_prague()
    {
        // 18:30 UTC is 19:30 in Prague in winter (CET).
        Assert.Equal("19:30", DisplayFormat.EventTime(new DateOnly(2026, 11, 3), new TimeOnly(18, 30)));
        Assert.Null(DisplayFormat.EventTime(new DateOnly(2026, 11, 3), null));
    }

    [Theory]
    [InlineData(2026, 10, 26, "Po 26. 10.")]
    [InlineData(2026, 11, 1, "Ne 1. 11.")]
    public void DayHeader_czech(int year, int month, int day, string expected) =>
        Assert.Equal(expected, DisplayFormat.DayHeader(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2026, 10, 26, "26. 10. – 1. 11. 2026")]
    [InlineData(2026, 12, 28, "28. 12. 2026 – 3. 1. 2027")]
    public void WeekLabel_same_and_cross_year(int year, int month, int day, string expected) =>
        Assert.Equal(expected, DisplayFormat.WeekLabel(new Week(new DateOnly(year, month, day))));

    [Fact]
    public void PlayerUrl_escapes_id_and_appends_week()
    {
        Assert.Equal("/players/a%2Fb%3Fc", DisplayFormat.PlayerUrl("a/b?c"));
        Assert.Equal("/players/abc?week=2026-10-26", DisplayFormat.PlayerUrl("abc", new Week(new DateOnly(2026, 10, 26))));
    }

    [Theory]
    [InlineData(1, "Ring of Baium")]
    [InlineData(2, "Ring of Baium ×2")]
    public void JewelTitle_adds_count_only_above_one(int count, string expected) =>
        Assert.Equal(expected, DisplayFormat.JewelTitle("Ring of Baium", count));

    [Theory]
    [InlineData(0, "0 kusů")]
    [InlineData(1, "1 kus")]
    [InlineData(2, "2 kusy")]
    [InlineData(4, "4 kusy")]
    [InlineData(5, "5 kusů")]
    [InlineData(22, "22 kusů")]
    public void Pieces_czech_plural(int count, string expected) =>
        Assert.Equal(expected, DisplayFormat.Pieces(count));

    [Theory]
    [InlineData(1, 1, 1, 8, "Celkem 1 kus u 1 hráče · 1 z 8 typů")]
    [InlineData(3, 2, 2, 8, "Celkem 3 kusy u 2 hráčů · 2 z 8 typů")]
    [InlineData(5, 5, 4, 8, "Celkem 5 kusů u 5 hráčů · 4 z 8 typů")]
    public void Jewel_gallery_summary(int pieces, int holders, int typesHeld, int typesTotal, string expected) =>
        Assert.Equal(expected, DisplayFormat.JewelGallerySummary(pieces, holders, typesHeld, typesTotal));

    [Fact]
    public void Jewel_gallery_summary_when_nobody_holds_a_jewel() =>
        Assert.Equal("Zatím nikdo nemá žádný šperk.", DisplayFormat.JewelGallerySummary(0, 0, 0, 8));

    private static readonly Week WinterWeek = Week.Containing(new DateOnly(2026, 11, 2));

    [Fact]
    public void Schedule_label_per_type()
    {
        var catalog = DefaultCatalog.CreateActivities().ToDictionary(a => a.Code);

        Assert.Equal("Flexibilní, 6× týdně", DisplayFormat.ScheduleLabel(catalog["QueenAnt"], WinterWeek));
        Assert.Equal("Týdně v sobotu 21:00", DisplayFormat.ScheduleLabel(catalog["Baium"], WinterWeek));
        Assert.Equal("Týdně v neděli 19:00", DisplayFormat.ScheduleLabel(catalog["CastleSiege"], WinterWeek));
        Assert.Equal("Ob týden v neděli", DisplayFormat.ScheduleLabel(catalog["Antharas"], WinterWeek));
        Assert.Equal("6 spawnů týdně", DisplayFormat.ScheduleLabel(catalog["ChaoticZaken"], WinterWeek));
        Assert.Equal("4 spawny týdně", DisplayFormat.ScheduleLabel(catalog["ChaoticFrintezza"], WinterWeek));
    }

    [Fact]
    public void Schedule_label_weekly_in_summer_time()
    {
        var baium = DefaultCatalog.CreateActivities().Single(a => a.Code == "Baium");

        Assert.Equal("Týdně v sobotu 22:00", DisplayFormat.ScheduleLabel(baium, Week.Containing(new DateOnly(2027, 6, 7))));
    }

    [Fact]
    public void Schedule_label_weekly_across_the_dst_switch()
    {
        var catalog = DefaultCatalog.CreateActivities().ToDictionary(a => a.Code);
        var week = Week.Containing(new DateOnly(2026, 10, 19));

        Assert.Equal("Týdně v sobotu 22:00", DisplayFormat.ScheduleLabel(catalog["Baium"], week));
        Assert.Equal("Týdně v neděli 19:00", DisplayFormat.ScheduleLabel(catalog["CastleSiege"], week));
    }

    [Fact]
    public void Schedule_label_weekly_with_utc()
    {
        var baium = DefaultCatalog.CreateActivities().Single(a => a.Code == "Baium");

        Assert.Equal("Týdně v sobotu 21:00 (20:00 UTC)", DisplayFormat.ScheduleLabel(baium, WinterWeek, withUtc: true));
    }

    [Fact]
    public void Schedule_label_weekly_without_time()
    {
        var activity = new Activity { ScheduleType = ScheduleType.Weekly, WeeklyDay = DayOfWeek.Saturday, TimeUtc = null };

        Assert.Equal("Týdně v sobotu", DisplayFormat.ScheduleLabel(activity, WinterWeek));
    }

    [Theory]
    [InlineData(1.0, "průměrně 1 hráč na epic")]
    [InlineData(1.5, "průměrně 1,5 hráče na epic")]
    [InlineData(3.0, "průměrně 3 hráči na epic")]
    [InlineData(5.0, "průměrně 5 hráčů na epic")]
    [InlineData(0.33, "průměrně 0,3 hráče na epic")]
    [InlineData(4.96, "průměrně 5 hráčů na epic")]
    public void AverageAttendees_czech_plural(double average, string expected) =>
        Assert.Equal(expected, DisplayFormat.AverageAttendees(average));

    [Fact]
    public void Catalog_message_covers_all_results()
    {
        foreach (var result in Enum.GetValues<CatalogChangeResult>())
        {
            Assert.False(string.IsNullOrWhiteSpace(DisplayFormat.CatalogMessage(result)), result.ToString());
        }

        Assert.Contains("22:00", DisplayFormat.CatalogMessage(CatalogChangeResult.InvalidSpawnTime));
    }

    [Theory]
    [InlineData(0, "0 spawnů týdně")]
    [InlineData(1, "1 spawn týdně")]
    [InlineData(2, "2 spawny týdně")]
    [InlineData(4, "4 spawny týdně")]
    [InlineData(5, "5 spawnů týdně")]
    public void Schedule_label_slotted_czech_plural(int spawns, string expected)
    {
        var activity = new Activity { ScheduleType = ScheduleType.Slotted };
        for (var i = 0; i < spawns; i++)
        {
            activity.Spawns.Add(new ActivitySpawn { DayOfWeek = DayOfWeek.Monday, Slot = EventSlot.Evening, TimeUtc = new TimeOnly(18, 0) });
        }

        Assert.Equal(expected, DisplayFormat.ScheduleLabel(activity, WinterWeek));
    }
}
