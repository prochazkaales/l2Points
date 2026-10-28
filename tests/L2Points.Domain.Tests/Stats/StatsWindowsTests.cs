using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class StatsWindowsTests
{
    private static readonly DateOnly TrackingStart = new(2026, 10, 26);

    [Fact]
    public void Overall_spans_tracking_start_to_today()
    {
        var window = StatsWindows.Overall(TrackingStart, new DateOnly(2026, 11, 4));

        Assert.Equal(new DateRange(TrackingStart, new DateOnly(2026, 11, 4)), window);
    }

    [Fact]
    public void Overall_is_null_before_tracking_start()
    {
        Assert.Null(StatsWindows.Overall(TrackingStart, new DateOnly(2026, 10, 20)));
    }
}
