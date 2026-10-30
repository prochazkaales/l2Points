using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;

namespace L2Points.Domain.Tests.Reports;

public class ReportEventPlannerTests
{
    private static readonly IReadOnlyList<Activity> Catalog = CreateCatalog();

    private static readonly string[] ChaoticCodes = ["ChaoticQueenAnt", "ChaoticCore", "ChaoticOrfen", "ChaoticZaken"];

    private static readonly IReadOnlySet<AttendanceEntry> NothingRecorded = new HashSet<AttendanceEntry>();

    private static IReadOnlyList<Activity> CreateCatalog()
    {
        var activities = DefaultCatalog.CreateActivities();
        for (var i = 0; i < activities.Count; i++)
            activities[i].Id = i + 1;
        return activities;
    }

    private static int IdOf(string code) => Catalog.Single(a => a.Code == code).Id;

    private static AttendanceEntry Entry(string code, DateOnly date, EventSlot slot = EventSlot.None) =>
        new(IdOf(code), date, slot);

    private static IEnumerable<AttendanceEntry> ChaoticEntries(DateOnly date, EventSlot slot) =>
        ChaoticCodes.Select(code => Entry(code, date, slot));

    [Fact]
    public void Friday_options_are_flexible_then_chaotic_by_slot()
    {
        var options = ReportEventPlanner.EventOptions(Catalog, new DateOnly(2026, 10, 30));

        Assert.Equal(13, options.Count);
        Assert.Equal("QueenAnt", options[0].Activity.Code);
        Assert.Null(options[0].SpawnUtc);

        var evening = options.ToList().FindIndex(o => o.Activity.Code == "ChaoticQueenAnt" && o.Event.Slot == EventSlot.Evening);
        Assert.True(evening >= 0);
        Assert.Equal("ChaoticQueenAnt", options[evening + 1].Activity.Code);
        Assert.Equal(EventSlot.Morning, options[evening + 1].Event.Slot);

        Assert.DoesNotContain(options, o => o.Activity.Code == "Baium");
    }

    [Fact]
    public void Sunday_options_include_antharas_without_time_and_castle_siege()
    {
        var options = ReportEventPlanner.EventOptions(Catalog, new DateOnly(2026, 11, 1));

        Assert.Null(options.Single(o => o.Activity.Code == "Antharas").SpawnUtc);
        Assert.Equal(
            new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc),
            options.Single(o => o.Activity.Code == "CastleSiege").SpawnUtc);
        Assert.DoesNotContain(options, o => o.Activity.Code == "Valakas");
    }

    [Fact]
    public void Evening_block_is_preselected()
    {
        var date = new DateOnly(2026, 10, 30);
        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-10-30T19:45:00Z"), NothingRecorded);

        Assert.Equal(4, result.Count);
        Assert.Equal(ChaoticEntries(date, EventSlot.Evening).ToHashSet(), result.ToHashSet());
    }

    [Fact]
    public void Future_spawns_are_not_preselected()
    {
        var date = new DateOnly(2026, 10, 30);
        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-10-30T18:40:00Z"), NothingRecorded);

        Assert.Equal(
            new[] { Entry("ChaoticQueenAnt", date, EventSlot.Evening), Entry("ChaoticCore", date, EventSlot.Evening) },
            result);
    }

    [Fact]
    public void Recorded_events_are_not_preselected()
    {
        var date = new DateOnly(2026, 10, 30);
        var recorded = new HashSet<AttendanceEntry> { Entry("ChaoticQueenAnt", date, EventSlot.Evening) };

        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-10-30T19:45:00Z"), recorded);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(Entry("ChaoticQueenAnt", date, EventSlot.Evening), result);
    }

    [Fact]
    public void Morning_block_does_not_reach_previous_evening()
    {
        var date = new DateOnly(2026, 10, 30);
        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-10-30T03:30:00Z"), NothingRecorded);

        Assert.Equal(4, result.Count);
        Assert.Equal(ChaoticEntries(date, EventSlot.Morning).ToHashSet(), result.ToHashSet());
    }

    [Fact]
    public void After_midnight_preselects_previous_evening_and_default_date()
    {
        var date = new DateOnly(2026, 10, 30);
        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-10-30T23:30:00Z"), NothingRecorded);

        Assert.Equal(4, result.Count);
        Assert.Equal(ChaoticEntries(date, EventSlot.Evening).ToHashSet(), result.ToHashSet());
        Assert.Equal(date, ReportEventPlanner.DefaultDate(result, new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Events_without_time_are_never_preselected()
    {
        var result = ReportEventPlanner.Preselect(Catalog, DateTimeOffset.Parse("2026-11-01T19:00:00Z"), NothingRecorded);

        var only = Assert.Single(result);
        Assert.Equal(IdOf("CastleSiege"), only.ActivityId);
    }

    [Fact]
    public void Default_date_without_preselection_is_today()
    {
        var today = new DateOnly(2026, 10, 30);

        Assert.Equal(today, ReportEventPlanner.DefaultDate([], today));
    }

    [Fact]
    public void Date_options_stop_at_tracking_start()
    {
        var today = new DateOnly(2026, 10, 30);

        var limited = ReportEventPlanner.DateOptions(today, new DateOnly(2026, 10, 26));
        Assert.Equal(
            new[] { 30, 29, 28, 27, 26 }.Select(d => new DateOnly(2026, 10, d)),
            limited);

        var full = ReportEventPlanner.DateOptions(today, new DateOnly(2026, 1, 1));
        Assert.Equal(7, full.Count);
        Assert.Equal(today, full[0]);
        Assert.Equal(new DateOnly(2026, 10, 24), full[^1]);
    }
}
