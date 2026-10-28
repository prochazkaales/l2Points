using L2Points.Domain.Activities;
using L2Points.Domain.Stats;

namespace L2Points.Domain.Tests.Stats;

public class PointsCalculatorTests
{
    private static readonly DateOnly Mon = new(2026, 10, 26);

    [Fact]
    public void Sum_uses_activity_points()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.Points = 3;
        AttendanceEntry[] attendances =
        [
            new(queenAnt.Id, Mon, EventSlot.None),
            new(queenAnt.Id, Mon.AddDays(1), EventSlot.None),
        ];

        Assert.Equal(6, PointsCalculator.Sum(activities, attendances));
    }

    [Fact]
    public void Sum_uses_slot_override()
    {
        var activities = StatsTestCatalog.Create();
        var chaotic = activities.ByCode("ChaoticQueenAnt");
        chaotic.Points = 2;
        chaotic.SlotPoints = [new ActivitySlotPoints { ActivityId = chaotic.Id, Slot = EventSlot.Morning, Points = 5 }];
        AttendanceEntry[] attendances =
        [
            new(chaotic.Id, Mon, EventSlot.Evening),
            new(chaotic.Id, Mon, EventSlot.Morning),
        ];

        Assert.Equal(7, PointsCalculator.Sum(activities, attendances));
    }

    [Fact]
    public void Sum_ignores_unknown_activity()
    {
        var activities = StatsTestCatalog.Create();
        AttendanceEntry[] attendances = [new(999, Mon, EventSlot.None)];

        Assert.Equal(0, PointsCalculator.Sum(activities, attendances));
    }

    [Fact]
    public void Sum_is_not_capped()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.Points = 1;
        var attendances = Enumerable.Range(0, 7)
            .Select(i => new AttendanceEntry(queenAnt.Id, Mon.AddDays(i), EventSlot.None))
            .ToList();

        Assert.Equal(7, PointsCalculator.Sum(activities, attendances));
    }

    [Fact]
    public void Sum_counts_inactive_activity()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.Points = 2;
        queenAnt.IsActive = false;
        AttendanceEntry[] attendances = [new(queenAnt.Id, Mon, EventSlot.None)];

        Assert.Equal(2, PointsCalculator.Sum(activities, attendances));
    }
}
