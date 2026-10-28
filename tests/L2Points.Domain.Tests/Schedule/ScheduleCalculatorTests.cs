using L2Points.Domain.Activities;
using L2Points.Domain.Schedule;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Schedule;

public class ScheduleCalculatorTests
{
    private static readonly IReadOnlyList<Activity> Catalog = DefaultCatalog.CreateActivities();

    private static Week WeekOf(int y, int m, int d) => Week.Containing(new DateOnly(y, m, d));

    private static List<ScheduledOccurrence> Occurrences(Week week) =>
        ScheduleCalculator.GetFixedOccurrences(Catalog, week.Range).ToList();

    [Fact]
    public void Week_of_2026_10_26_has_40_chaotic_occurrences()
    {
        var chaotic = Occurrences(WeekOf(2026, 10, 26)).Where(o => o.Activity.Category == ActivityCategory.ChaoticEpic).ToList();

        Assert.Equal(40, chaotic.Count);
        Assert.Equal(20, chaotic.Count(o => o.Slot == EventSlot.Evening));
        Assert.Equal(20, chaotic.Count(o => o.Slot == EventSlot.Morning));
    }

    [Fact]
    public void Antharas_on_2026_11_01_and_valakas_next_week()
    {
        var week1 = Occurrences(WeekOf(2026, 10, 26));
        Assert.Contains(week1, o => o.Activity.Code == "Antharas" && o.Date == new DateOnly(2026, 11, 1));
        Assert.DoesNotContain(week1, o => o.Activity.Code == "Valakas");

        var week2 = Occurrences(WeekOf(2026, 11, 2));
        Assert.Contains(week2, o => o.Activity.Code == "Valakas" && o.Date == new DateOnly(2026, 11, 8));
        Assert.DoesNotContain(week2, o => o.Activity.Code == "Antharas");
    }

    [Fact]
    public void Valakas_before_anchor_uses_positive_modulo()
    {
        var week = Occurrences(WeekOf(2026, 10, 19));

        Assert.Contains(week, o => o.Activity.Code == "Valakas" && o.Date == new DateOnly(2026, 10, 25));
        Assert.DoesNotContain(week, o => o.Activity.Code == "Antharas");
    }

    [Fact]
    public void Baium_saturday_and_castle_siege_sunday()
    {
        var week = Occurrences(WeekOf(2026, 10, 26));

        var baium = Assert.Single(week, o => o.Activity.Code == "Baium");
        Assert.Equal(new DateOnly(2026, 10, 31), baium.Date);
        Assert.Equal(new TimeOnly(20, 0), baium.TimeUtc);
        Assert.Equal(EventSlot.None, baium.Slot);

        var siege = Assert.Single(week, o => o.Activity.Code == "CastleSiege");
        Assert.Equal(new DateOnly(2026, 11, 1), siege.Date);
        Assert.Equal(new TimeOnly(18, 0), siege.TimeUtc);
    }

    [Fact]
    public void Dst_week_keeps_40_chaotic_occurrences()
    {
        var chaotic = Occurrences(WeekOf(2027, 3, 28)).Where(o => o.Activity.Category == ActivityCategory.ChaoticEpic).ToList();

        Assert.Equal(40, chaotic.Count);
    }

    [Fact]
    public void Flexible_bosses_have_no_fixed_occurrences()
    {
        var flexible = Catalog.Where(a => a.ScheduleType == ScheduleType.Flexible);

        Assert.Empty(ScheduleCalculator.GetFixedOccurrences(flexible, WeekOf(2026, 10, 26).Range));
    }

    [Fact]
    public void Inactive_activity_has_no_occurrences()
    {
        var baium = DefaultCatalog.CreateActivities().Single(a => a.Code == "Baium");
        baium.IsActive = false;

        Assert.Empty(ScheduleCalculator.GetFixedOccurrences([baium], WeekOf(2026, 10, 26).Range));
    }

    [Fact]
    public void Occurrences_are_ordered_by_date_sort_order_slot()
    {
        var week = Occurrences(WeekOf(2026, 10, 26));

        var sorted = week.OrderBy(o => o.Date).ThenBy(o => o.Activity.SortOrder).ThenBy(o => o.Slot).ToList();
        Assert.Equal(sorted, week);
    }

    [Fact]
    public void Range_is_respected_for_partial_ranges()
    {
        var range = new DateRange(new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 1));

        var result = ScheduleCalculator.GetFixedOccurrences(Catalog, range);

        Assert.All(result, o => Assert.True(range.Contains(o.Date)));
        Assert.Contains(result, o => o.Activity.Code == "Baium");
        Assert.Contains(result, o => o.Activity.Code == "CastleSiege");
    }

    [Fact]
    public void IsValidSlot_chaotic_zaken()
    {
        var zaken = Catalog.Single(a => a.Code == "ChaoticZaken");
        var monday = new DateOnly(2026, 10, 26);
        var tuesday = new DateOnly(2026, 10, 27);

        Assert.True(ScheduleCalculator.IsValidSlot(zaken, monday, EventSlot.Evening));
        Assert.True(ScheduleCalculator.IsValidSlot(zaken, monday, EventSlot.Morning));
        Assert.False(ScheduleCalculator.IsValidSlot(zaken, monday, EventSlot.None));
        Assert.False(ScheduleCalculator.IsValidSlot(zaken, tuesday, EventSlot.Evening));
    }

    [Fact]
    public void IsValidSlot_flexible_any_day_without_slot()
    {
        var queenAnt = Catalog.Single(a => a.Code == "QueenAnt");
        var sunday = new DateOnly(2026, 11, 1);

        Assert.True(ScheduleCalculator.IsValidSlot(queenAnt, sunday, EventSlot.None));
        Assert.False(ScheduleCalculator.IsValidSlot(queenAnt, sunday, EventSlot.Evening));
    }

    [Fact]
    public void IsValidSlot_weekly_and_biweekly()
    {
        var baium = Catalog.Single(a => a.Code == "Baium");
        var antharas = Catalog.Single(a => a.Code == "Antharas");

        Assert.True(ScheduleCalculator.IsValidSlot(baium, new DateOnly(2026, 10, 31), EventSlot.None));
        Assert.False(ScheduleCalculator.IsValidSlot(baium, new DateOnly(2026, 11, 1), EventSlot.None));
        Assert.True(ScheduleCalculator.IsValidSlot(antharas, new DateOnly(2026, 11, 1), EventSlot.None));
        Assert.False(ScheduleCalculator.IsValidSlot(antharas, new DateOnly(2026, 11, 8), EventSlot.None));
    }

    [Fact]
    public void IsValidSlot_inactive_is_false()
    {
        var queenAnt = DefaultCatalog.CreateActivities().Single(a => a.Code == "QueenAnt");
        queenAnt.IsActive = false;

        Assert.False(ScheduleCalculator.IsValidSlot(queenAnt, new DateOnly(2026, 10, 26), EventSlot.None));
    }
}
