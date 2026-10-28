using L2Points.Domain.Time;

namespace L2Points.Domain.Tests.Time;

public class WeekTests
{
    [Fact]
    public void Containing_returns_monday()
    {
        var week = Week.Containing(new DateOnly(2026, 10, 28));

        Assert.Equal(new DateOnly(2026, 10, 26), week.Monday);
        Assert.Equal(new DateOnly(2026, 11, 1), week.Sunday);
    }

    [Fact]
    public void Containing_sunday_stays_in_same_week()
    {
        Assert.Equal(new DateOnly(2026, 10, 26), Week.Containing(new DateOnly(2026, 11, 1)).Monday);
    }

    [Fact]
    public void Days_are_seven_consecutive()
    {
        var days = Week.Containing(new DateOnly(2026, 10, 28)).Days.ToList();

        Assert.Equal(7, days.Count);
        Assert.Equal(new DateOnly(2026, 10, 26), days.First());
        Assert.Equal(new DateOnly(2026, 11, 1), days.Last());
    }

    [Fact]
    public void Next_and_previous()
    {
        var week = Week.Containing(new DateOnly(2026, 10, 28));

        Assert.Equal(new DateOnly(2026, 11, 2), week.Next().Monday);
        Assert.Equal(new DateOnly(2026, 10, 19), week.Previous().Monday);
    }

    [Fact]
    public void ParseOrCurrent_normalizes_to_monday()
    {
        var week = Week.ParseOrCurrent("2026-10-29", new DateOnly(2026, 11, 4));

        Assert.Equal(new DateOnly(2026, 10, 26), week.Monday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2026-13-01")]
    public void ParseOrCurrent_falls_back_to_today(string? input)
    {
        var week = Week.ParseOrCurrent(input, new DateOnly(2026, 11, 4));

        Assert.Equal(new DateOnly(2026, 11, 2), week.Monday);
    }
}
