using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Time;

public class DateRangeTests
{
    [Fact]
    public void Ctor_throws_when_from_after_to()
    {
        Assert.Throws<ArgumentException>(() => new DateRange(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public void DayCount_is_inclusive()
    {
        Assert.Equal(1, new DateRange(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 26)).DayCount);
        Assert.Equal(7, Week.Containing(new DateOnly(2026, 10, 28)).Range.DayCount);
    }

    [Fact]
    public void Intersect_returns_overlap_or_null()
    {
        var a = new DateRange(new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1));
        var b = new DateRange(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 5));
        var far = new DateRange(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12));

        Assert.Equal(new DateRange(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 1)), a.Intersect(b));
        Assert.Null(a.Intersect(far));
    }

    [Fact]
    public void Contains_is_inclusive_on_both_ends()
    {
        var range = new DateRange(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 28));

        Assert.True(range.Contains(new DateOnly(2026, 10, 26)));
        Assert.True(range.Contains(new DateOnly(2026, 10, 28)));
        Assert.False(range.Contains(new DateOnly(2026, 10, 25)));
        Assert.False(range.Contains(new DateOnly(2026, 10, 29)));
    }

    [Fact]
    public void Weeks_lists_all_touched_weeks()
    {
        var range = new DateRange(new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 3));

        var mondays = range.Weeks.Select(w => w.Monday).ToList();

        Assert.Equal([new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 2)], mondays);
    }
}
