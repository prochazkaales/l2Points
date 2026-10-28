using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Domain.Stats;
using L2Points.Domain.Tests.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Attendances;

public class WeekGridBuilderTests
{
    private static readonly Week TestWeek = Week.Containing(new DateOnly(2026, 10, 28));
    private static readonly DateOnly Today = new(2026, 10, 28);
    private static readonly DateOnly TrackingStart = new(2026, 10, 26);

    private static WeekGrid Build(
        List<Activity> activities,
        IReadOnlyCollection<AttendanceEntry>? attendances = null,
        Week? week = null,
        DateOnly? today = null,
        bool isAdmin = false) =>
        WeekGridBuilder.Build(activities, week ?? TestWeek, attendances ?? [], today ?? Today, TrackingStart, isAdmin);

    private static WeekGridRow Row(WeekGrid grid, string code) =>
        grid.Rows.Single(r => r.Activity.Code == code);

    [Fact]
    public void Rows_are_active_activities_in_sort_order()
    {
        var grid = Build(StatsTestCatalog.Create());

        Assert.Equal(14, grid.Rows.Count);
        Assert.Equal("QueenAnt", grid.Rows[0].Activity.Code);
        Assert.Equal("CastleSiege", grid.Rows[^1].Activity.Code);
        Assert.Equal(TestWeek, grid.Week);
    }

    [Fact]
    public void Flexible_row_has_cell_every_day()
    {
        var cells = Row(Build(StatsTestCatalog.Create()), "QueenAnt").Cells;

        Assert.Equal(7, cells.Count);
        Assert.All(cells, c => Assert.Equal(EventSlot.None, c.Slot));
        Assert.All(cells, c => Assert.True(c.IsScheduled));
        Assert.Equal(TestWeek.Days, cells.Select(c => c.Date));
    }

    [Fact]
    public void Chaotic_zaken_cells()
    {
        var cells = Row(Build(StatsTestCatalog.Create()), "ChaoticZaken").Cells;

        Assert.Equal(6, cells.Count);
        Assert.Equal(
            [
                (new DateOnly(2026, 10, 26), EventSlot.Evening),
                (new DateOnly(2026, 10, 26), EventSlot.Morning),
                (new DateOnly(2026, 10, 28), EventSlot.Evening),
                (new DateOnly(2026, 10, 28), EventSlot.Morning),
                (new DateOnly(2026, 10, 30), EventSlot.Evening),
                (new DateOnly(2026, 10, 30), EventSlot.Morning),
            ],
            cells.Select(c => (c.Date, c.Slot)));
        var mondayEvening = cells.Single(c => c.Date == new DateOnly(2026, 10, 26) && c.Slot == EventSlot.Evening);
        Assert.Equal(new TimeOnly(19, 30), mondayEvening.TimeUtc);
        Assert.All(cells, c => Assert.True(c.IsScheduled));
    }

    [Fact]
    public void Antharas_only_on_sunday_and_valakas_has_no_cells()
    {
        var grid = Build(StatsTestCatalog.Create());

        var antharas = Assert.Single(Row(grid, "Antharas").Cells);
        Assert.Equal(new DateOnly(2026, 11, 1), antharas.Date);
        Assert.Empty(Row(grid, "Valakas").Cells);
    }

    [Fact]
    public void Checked_cells_reflect_attendance()
    {
        var activities = StatsTestCatalog.Create();
        var queenAntId = activities.ByCode("QueenAnt").Id;
        var attendances = new[] { new AttendanceEntry(queenAntId, new DateOnly(2026, 10, 27), EventSlot.None) };

        var cells = Row(Build(activities, attendances), "QueenAnt").Cells;

        Assert.Equal([new DateOnly(2026, 10, 27)], cells.Where(c => c.IsChecked).Select(c => c.Date));
        Assert.Equal(7, cells.Count);
    }

    [Fact]
    public void Editable_only_in_window()
    {
        var cells = Row(Build(StatsTestCatalog.Create()), "QueenAnt").Cells;

        Assert.Equal(
            [true, true, true, false, false, false, false],
            cells.Select(c => c.IsEditable));
    }

    [Fact]
    public void Inactive_activity_with_attendance_is_shown_as_orphan()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.IsActive = false;
        var attendances = new[] { new AttendanceEntry(queenAnt.Id, new DateOnly(2026, 10, 27), EventSlot.None) };

        var grid = Build(activities, attendances);

        Assert.Equal(14, grid.Rows.Count);
        var cell = Assert.Single(Row(grid, "QueenAnt").Cells);
        Assert.Equal(new DateOnly(2026, 10, 27), cell.Date);
        Assert.False(cell.IsScheduled);
        Assert.True(cell.IsChecked);
        Assert.True(cell.IsEditable);
    }

    [Fact]
    public void Inactive_activity_without_attendance_hidden()
    {
        var activities = StatsTestCatalog.Create();
        activities.ByCode("QueenAnt").IsActive = false;

        var grid = Build(activities);

        Assert.Equal(13, grid.Rows.Count);
        Assert.DoesNotContain(grid.Rows, r => r.Activity.Code == "QueenAnt");
    }

    [Fact]
    public void Admin_can_edit_older_days()
    {
        var activities = StatsTestCatalog.Create();
        var today = new DateOnly(2026, 11, 5);
        var monday = new DateOnly(2026, 10, 26);

        var admin = Row(Build(activities, today: today, isAdmin: true), "QueenAnt").Cells.Single(c => c.Date == monday);
        var player = Row(Build(activities, today: today), "QueenAnt").Cells.Single(c => c.Date == monday);

        Assert.True(admin.IsEditable);
        Assert.False(player.IsEditable);
    }

    [Fact]
    public void Unscheduled_attendance_on_active_activity_becomes_orphan_cell()
    {
        var activities = StatsTestCatalog.Create();
        var valakas = activities.ByCode("Valakas");
        var attendances = new[] { new AttendanceEntry(valakas.Id, new DateOnly(2026, 10, 27), EventSlot.None) };

        var cell = Assert.Single(Row(Build(activities, attendances), "Valakas").Cells);

        Assert.False(cell.IsScheduled);
        Assert.True(cell.IsChecked);
        Assert.True(cell.IsEditable);
    }

    [Fact]
    public void Unscheduled_orphan_outside_window_is_not_editable()
    {
        var activities = StatsTestCatalog.Create();
        var valakas = activities.ByCode("Valakas");
        var attendances = new[] { new AttendanceEntry(valakas.Id, new DateOnly(2026, 10, 27), EventSlot.None) };

        var cell = Assert.Single(Row(Build(activities, attendances, today: new DateOnly(2026, 11, 5)), "Valakas").Cells);

        Assert.False(cell.IsEditable);
    }

    [Fact]
    public void Attendance_outside_week_is_ignored()
    {
        var activities = StatsTestCatalog.Create();
        var valakas = activities.ByCode("Valakas");
        var attendances = new[] { new AttendanceEntry(valakas.Id, new DateOnly(2026, 10, 20), EventSlot.None) };

        Assert.Empty(Row(Build(activities, attendances), "Valakas").Cells);
    }
}
