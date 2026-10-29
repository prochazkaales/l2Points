using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class PlayerHistoryCalculatorTests
{
    private static readonly DateOnly TrackingStart = new(2026, 10, 26);
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Fact]
    public void History_newest_first()
    {
        var activities = StatsTestCatalog.Create();
        var player = new PlayerInfo("A", "A", true, null);
        AttendanceEntry[] attendances =
        [
            new(activities.ByCode("QueenAnt").Id, new DateOnly(2026, 10, 26), EventSlot.None),
            new(activities.ByCode("ChaoticCore").Id, new DateOnly(2026, 10, 27), EventSlot.Evening),
            new(activities.ByCode("CastleSiege").Id, new DateOnly(2026, 11, 1), EventSlot.None),
        ];

        var rows = PlayerHistoryCalculator.Build(player, activities, attendances, TrackingStart, Today);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 11, 2), rows[0].Week.Monday);
        Assert.Equal(0, rows[0].Summary.Points);
        Assert.Equal(new CategoryStat(0, 35), rows[0].Summary.Stats.Total);
        Assert.Equal(new DateOnly(2026, 10, 26), rows[1].Week.Monday);
        Assert.Equal(3, rows[1].Summary.Points);
        Assert.Equal(new CategoryStat(3, 64), rows[1].Summary.Stats.Total);
    }

    [Fact]
    public void History_of_deactivated_player()
    {
        var activities = StatsTestCatalog.Create();
        var player = new PlayerInfo("D", "D", false, new DateOnly(2026, 10, 27));

        var rows = PlayerHistoryCalculator.Build(player, activities, [], TrackingStart, Today);

        Assert.Equal(25, rows[1].Summary.Stats.Total.Expected);
        Assert.Equal(new CategoryStat(0, 0), rows[0].Summary.Stats.Total);
        Assert.Null(rows[0].Summary.Stats.Total.Percent);
    }

    [Fact]
    public void History_empty_before_tracking_start()
    {
        var activities = StatsTestCatalog.Create();
        var player = new PlayerInfo("A", "A", true, null);

        var rows = PlayerHistoryCalculator.Build(player, activities, [], TrackingStart, new DateOnly(2026, 10, 20));

        Assert.Empty(rows);
    }
}
