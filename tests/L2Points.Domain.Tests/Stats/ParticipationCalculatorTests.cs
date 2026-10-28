using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class ParticipationCalculatorTests
{
    private static readonly DateOnly Mon = new(2026, 10, 26);
    private static readonly DateOnly Sun = new(2026, 11, 1);

    private static DateRange Range(DateOnly from, DateOnly to) => new(from, to);

    [Fact]
    public void Full_week_expected_counts()
    {
        var activities = StatsTestCatalog.Create();

        var stats = ParticipationCalculator.Calculate(activities, [], Range(Mon, Sun));

        Assert.Equal(new CategoryStat(0, 64), stats.Total);
        Assert.Equal(new CategoryStat(0, 23), stats[StatCategory.Normal]);
        Assert.Equal(new CategoryStat(0, 20), stats[StatCategory.Evening]);
        Assert.Equal(new CategoryStat(0, 20), stats[StatCategory.Morning]);
        Assert.Equal(new CategoryStat(0, 1), stats[StatCategory.CastleSiege]);
        Assert.Equal(0.0, stats.Total.Percent);
    }

    [Fact]
    public void Monday_only_prorates_flexible()
    {
        var activities = StatsTestCatalog.Create();

        var stats = ParticipationCalculator.Calculate(activities, [], Range(Mon, Mon));

        Assert.Equal(5, stats[StatCategory.Normal].Expected);
        Assert.Equal(4, stats[StatCategory.Evening].Expected);
        Assert.Equal(4, stats[StatCategory.Morning].Expected);
        Assert.Equal(0, stats[StatCategory.CastleSiege].Expected);
        Assert.Equal(13, stats.Total.Expected);
    }

    [Fact]
    public void Two_weeks_alternate_antharas_valakas()
    {
        var activities = StatsTestCatalog.Create();

        var stats = ParticipationCalculator.Calculate(activities, [], Range(Mon, new DateOnly(2026, 11, 8)));

        Assert.Equal(128, stats.Total.Expected);
        Assert.Equal(46, stats[StatCategory.Normal].Expected);
    }

    [Fact]
    public void Attendance_is_capped_per_bucket()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        var attendances = Enumerable.Range(0, 7)
            .Select(i => new AttendanceEntry(queenAnt.Id, Mon.AddDays(i), EventSlot.None))
            .ToList();

        var stats = ParticipationCalculator.Calculate(activities, attendances, Range(Mon, Sun));

        Assert.Equal(6, stats[StatCategory.Normal].Attended);
    }

    [Fact]
    public void Chaotic_attendance_counts_in_slot_category()
    {
        var activities = StatsTestCatalog.Create();
        var core = activities.ByCode("ChaoticCore");
        var tuesday = new DateOnly(2026, 10, 27);
        AttendanceEntry[] attendances =
        [
            new(core.Id, tuesday, EventSlot.Evening),
            new(core.Id, tuesday, EventSlot.Morning),
        ];

        var stats = ParticipationCalculator.Calculate(activities, attendances, Range(Mon, Sun));

        Assert.Equal(1, stats[StatCategory.Evening].Attended);
        Assert.Equal(1, stats[StatCategory.Morning].Attended);
    }

    [Fact]
    public void Attendance_outside_range_is_ignored()
    {
        var activities = StatsTestCatalog.Create();
        var siege = activities.ByCode("CastleSiege");
        AttendanceEntry[] attendances = [new(siege.Id, new DateOnly(2026, 11, 8), EventSlot.None)];

        var stats = ParticipationCalculator.Calculate(activities, attendances, Range(Mon, Sun));

        Assert.Equal(0, stats[StatCategory.CastleSiege].Attended);
    }

    [Fact]
    public void Inactive_activity_excluded()
    {
        var activities = StatsTestCatalog.Create();
        var queenAnt = activities.ByCode("QueenAnt");
        queenAnt.IsActive = false;
        AttendanceEntry[] attendances = [new(queenAnt.Id, Mon, EventSlot.None)];

        var stats = ParticipationCalculator.Calculate(activities, attendances, Range(Mon, Sun));

        Assert.Equal(17, stats[StatCategory.Normal].Expected);
        Assert.Equal(0, stats[StatCategory.Normal].Attended);
    }

    [Fact]
    public void Null_range_returns_empty()
    {
        var activities = StatsTestCatalog.Create();

        var stats = ParticipationCalculator.Calculate(activities, [], null);

        Assert.Equal(new CategoryStat(0, 0), stats.Total);
        Assert.Null(stats.Total.Percent);
    }

    [Fact]
    public void Percent_value()
    {
        Assert.Equal(25.0, new CategoryStat(1, 4).Percent);
    }
}
