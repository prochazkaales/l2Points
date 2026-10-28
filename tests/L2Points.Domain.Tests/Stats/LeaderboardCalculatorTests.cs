using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class LeaderboardCalculatorTests
{
    private static readonly DateOnly TrackingStart = new(2026, 10, 26);
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly Week CurrentWeek = Week.Containing(Today);

    private static PlayerInfo Player(string id, string nick, DateOnly? activeFrom = null, DateOnly? deactivatedAt = null) =>
        new(id, nick, deactivatedAt is null, activeFrom ?? TrackingStart, deactivatedAt);

    private static List<UserAttendance> AttendancesOfA(IReadOnlyList<Activity> activities) =>
    [
        new("A", new(activities.ByCode("QueenAnt").Id, new DateOnly(2026, 10, 26), EventSlot.None)),
        new("A", new(activities.ByCode("ChaoticCore").Id, new DateOnly(2026, 10, 27), EventSlot.Evening)),
        new("A", new(activities.ByCode("CastleSiege").Id, new DateOnly(2026, 11, 1), EventSlot.None)),
    ];

    [Fact]
    public void Overall_numbers_for_active_player()
    {
        var activities = StatsTestCatalog.Create();

        var rows = LeaderboardCalculator.Build([Player("A", "A")], activities, AttendancesOfA(activities), CurrentWeek, TrackingStart, Today);

        var overall = Assert.Single(rows).Overall;
        Assert.Equal(3, overall.Points);
        Assert.Equal(new CategoryStat(3, 99), overall.Stats.Total);
        Assert.Equal(new CategoryStat(1, 34), overall.Stats[StatCategory.Normal]);
        Assert.Equal(new CategoryStat(1, 32), overall.Stats[StatCategory.Evening]);
        Assert.Equal(new CategoryStat(0, 32), overall.Stats[StatCategory.Morning]);
        Assert.Equal(new CategoryStat(1, 1), overall.Stats[StatCategory.CastleSiege]);
    }

    [Fact]
    public void Week_numbers_for_current_week()
    {
        var activities = StatsTestCatalog.Create();

        var rows = LeaderboardCalculator.Build([Player("A", "A")], activities, AttendancesOfA(activities), CurrentWeek, TrackingStart, Today);

        var week = Assert.Single(rows).Week;
        Assert.Equal(0, week.Points);
        Assert.Equal(new CategoryStat(0, 35), week.Stats.Total);
    }

    [Fact]
    public void Late_joiner_counts_from_active_from()
    {
        var activities = StatsTestCatalog.Create();

        var rows = LeaderboardCalculator.Build([Player("B", "B", new DateOnly(2026, 11, 2))], activities, [], CurrentWeek, TrackingStart, Today);

        Assert.Equal(new CategoryStat(0, 35), Assert.Single(rows).Overall.Stats.Total);
    }

    [Fact]
    public void Deactivated_player_frozen()
    {
        var activities = StatsTestCatalog.Create();

        var rows = LeaderboardCalculator.Build([Player("D", "D", deactivatedAt: new DateOnly(2026, 10, 27))], activities, [], CurrentWeek, TrackingStart, Today);

        Assert.Equal(25, Assert.Single(rows).Overall.Stats.Total.Expected);
    }

    [Fact]
    public void Ordered_by_points_then_nick_ignore_case()
    {
        var activities = StatsTestCatalog.Create();
        PlayerInfo[] players = [Player("X", "Bob"), Player("Y", "alice"), Player("A", "A")];

        var rows = LeaderboardCalculator.Build(players, activities, AttendancesOfA(activities), CurrentWeek, TrackingStart, Today);

        Assert.Equal(["A", "alice", "Bob"], rows.Select(r => r.Player.Nick));
    }

    [Fact]
    public void Before_tracking_start_everything_empty()
    {
        var activities = StatsTestCatalog.Create();
        var today = new DateOnly(2026, 10, 20);

        var rows = LeaderboardCalculator.Build([Player("A", "A")], activities, [], Week.Containing(today), TrackingStart, today);

        var row = Assert.Single(rows);
        Assert.Equal(0, row.Overall.Points);
        Assert.Equal(new CategoryStat(0, 0), row.Overall.Stats.Total);
        Assert.Equal(new CategoryStat(0, 0), row.Week.Stats.Total);
    }
}
