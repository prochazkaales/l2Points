using L2Points.Domain.Time;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Domain.Tests.Time;

public class AppTimeTests
{
    [Fact]
    public void TodayInPrague_uses_prague_calendar()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-28T23:30:00Z"));

        Assert.Equal(new DateOnly(2026, 10, 29), AppTime.TodayInPrague(time));
    }

    [Fact]
    public void ToPragueTime_in_winter_adds_one_hour()
    {
        var result = AppTime.ToPragueTime(new DateOnly(2026, 10, 26), new TimeOnly(18, 0));

        Assert.Equal(new DateTime(2026, 10, 26, 19, 0, 0), result);
    }

    [Fact]
    public void ToPragueTime_in_summer_adds_two_hours()
    {
        var result = AppTime.ToPragueTime(new DateOnly(2026, 10, 20), new TimeOnly(18, 0));

        Assert.Equal(new DateTime(2026, 10, 20, 20, 0, 0), result);
    }

    [Fact]
    public void ToPragueTime_across_spring_dst()
    {
        Assert.Equal(new DateTime(2027, 3, 26, 19, 0, 0),
            AppTime.ToPragueTime(new DateOnly(2027, 3, 26), new TimeOnly(18, 0)));
        Assert.Equal(new DateTime(2027, 3, 29, 20, 0, 0),
            AppTime.ToPragueTime(new DateOnly(2027, 3, 29), new TimeOnly(18, 0)));
    }
}
