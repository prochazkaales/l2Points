using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Stats;

public class PlayerPeriodTests
{
    private static readonly DateOnly Tracking = new(2026, 10, 26);
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Fact]
    public void Resolve_takes_later_start()
    {
        var range = PlayerPeriod.Resolve(Tracking, new DateOnly(2026, 11, 2), null, Today);

        Assert.Equal(new DateRange(new DateOnly(2026, 11, 2), Today), range);
    }

    [Fact]
    public void Resolve_stops_at_deactivation()
    {
        var range = PlayerPeriod.Resolve(Tracking, Tracking, new DateOnly(2026, 11, 4), Today);

        Assert.Equal(new DateRange(Tracking, new DateOnly(2026, 11, 4)), range);
    }

    [Fact]
    public void Resolve_returns_null_when_empty()
    {
        Assert.Null(PlayerPeriod.Resolve(Tracking, new DateOnly(2026, 11, 20), null, Today));
        Assert.Null(PlayerPeriod.Resolve(Tracking, Tracking, new DateOnly(2026, 10, 20), Today));
    }
}
