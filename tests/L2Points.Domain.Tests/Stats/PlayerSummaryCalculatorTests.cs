using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class PlayerSummaryCalculatorTests
{
    private static readonly DateRange Window = new(new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1));

    [Fact]
    public void Week_summary_counts_points_in_window_only()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        AttendanceEntry[] attendances =
        [
            new(queenAnt.Id, new DateOnly(2026, 10, 26), EventSlot.None),
            new(queenAnt.Id, new DateOnly(2026, 11, 2), EventSlot.None),
        ];

        var summary = PlayerSummaryCalculator.Calculate(activities, attendances, Window, Window);

        Assert.Equal(1, summary.Points);
    }

    [Fact]
    public void Week_summary_prorated_to_today()
    {
        var activities = StatsTestCatalog.Create();
        var period = new DateRange(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 28));

        var summary = PlayerSummaryCalculator.Calculate(activities, [], Window, period);

        Assert.Equal(35, summary.Stats.Total.Expected);
        Assert.Equal(11, summary.Stats[StatCategory.Normal].Expected);
        Assert.Equal(12, summary.Stats[StatCategory.Evening].Expected);
        Assert.Equal(12, summary.Stats[StatCategory.Morning].Expected);
        Assert.Equal(0, summary.Stats[StatCategory.CastleSiege].Expected);
    }

    [Fact]
    public void Null_period_gives_empty_stats_but_points()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        AttendanceEntry[] attendances = [new(queenAnt.Id, new DateOnly(2026, 10, 27), EventSlot.None)];

        var summary = PlayerSummaryCalculator.Calculate(activities, attendances, Window, playerPeriod: null);

        Assert.Equal(1, summary.Points);
        Assert.Equal(new CategoryStat(0, 0), summary.Stats.Total);
    }
}
