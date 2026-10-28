using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class WeekEventsCalculatorTests
{
    private static readonly Week TestWeek = Week.Containing(new DateOnly(2026, 10, 26));

    private static PlayerInfo Player(string id, string nick) =>
        new(id, nick, true, new DateOnly(2026, 10, 26), null);

    private static UserAttendance Attendance(string userId, Activity activity, DateOnly date, EventSlot slot = EventSlot.None) =>
        new(userId, new AttendanceEntry(activity.Id, date, slot));

    [Fact]
    public void Groups_attendees_per_event()
    {
        var activities = StatsTestCatalog.Create();
        var core = activities.ByCode("ChaoticCore");
        var day = new DateOnly(2026, 10, 27);
        PlayerInfo[] players = [Player("U1", "bob"), Player("U2", "Anna")];
        UserAttendance[] attendances =
        [
            Attendance("U1", core, day, EventSlot.Evening),
            Attendance("U2", core, day, EventSlot.Evening),
        ];

        var events = WeekEventsCalculator.Build(players, activities, attendances, TestWeek);

        var weekEvent = Assert.Single(events);
        Assert.Equal(["Anna", "bob"], weekEvent.Attendees.Select(p => p.Nick));
        Assert.Equal(new TimeOnly(18, 30), weekEvent.TimeUtc);
    }

    [Fact]
    public void Flexible_event_has_no_time()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");

        var events = WeekEventsCalculator.Build(
            [Player("A", "A")], activities, [Attendance("A", queenAnt, new DateOnly(2026, 10, 26))], TestWeek);

        var weekEvent = Assert.Single(events);
        Assert.Null(weekEvent.TimeUtc);
        Assert.Equal(EventSlot.None, weekEvent.Slot);
    }

    [Fact]
    public void Ordered_by_date_sort_order_slot()
    {
        var activities = StatsTestCatalog.Create();
        var core = activities.ByCode("ChaoticCore");
        var queenAnt = activities.ByCode("QueenAnt");
        var day = new DateOnly(2026, 10, 27);
        UserAttendance[] attendances =
        [
            Attendance("A", core, day, EventSlot.Morning),
            Attendance("A", queenAnt, day),
            Attendance("A", core, day, EventSlot.Evening),
            Attendance("A", queenAnt, new DateOnly(2026, 10, 26)),
        ];

        var events = WeekEventsCalculator.Build([Player("A", "A")], activities, attendances, TestWeek);

        Assert.Equal(
            [
                (queenAnt.Id, new DateOnly(2026, 10, 26), EventSlot.None),
                (queenAnt.Id, day, EventSlot.None),
                (core.Id, day, EventSlot.Evening),
                (core.Id, day, EventSlot.Morning),
            ],
            events.Select(e => (e.Activity.Id, e.Date, e.Slot)));
    }

    [Fact]
    public void Outside_week_and_unknown_ignored()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        UserAttendance[] attendances =
        [
            Attendance("A", queenAnt, new DateOnly(2026, 11, 2)),
            Attendance("unknown", queenAnt, new DateOnly(2026, 10, 27)),
            new("A", new AttendanceEntry(9999, new DateOnly(2026, 10, 27), EventSlot.None)),
        ];

        var events = WeekEventsCalculator.Build([Player("A", "A")], activities, attendances, TestWeek);

        Assert.Empty(events);
    }

    [Fact]
    public void Inactive_activity_history_is_listed()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.IsActive = false;

        var events = WeekEventsCalculator.Build(
            [Player("A", "A")], activities, [Attendance("A", queenAnt, new DateOnly(2026, 10, 27))], TestWeek);

        var weekEvent = Assert.Single(events);
        Assert.Null(weekEvent.TimeUtc);
    }
}
