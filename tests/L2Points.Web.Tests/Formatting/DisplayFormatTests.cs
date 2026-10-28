using L2Points.Domain.Time;
using L2Points.Web.Formatting;

namespace L2Points.Web.Tests.Formatting;

public class DisplayFormatTests
{
    [Theory]
    [InlineData(44.5, "45 %")]
    [InlineData(44.4, "44 %")]
    [InlineData(0.0, "0 %")]
    [InlineData(100.0, "100 %")]
    public void Percent_rounds_half_away_from_zero(double value, string expected) =>
        Assert.Equal(expected, DisplayFormat.Percent(value));

    [Fact]
    public void Percent_null_is_dash() => Assert.Equal("–", DisplayFormat.Percent(null));

    [Theory]
    [InlineData(2026, 10, 26, "Po 26. 10.")]
    [InlineData(2026, 11, 1, "Ne 1. 11.")]
    public void DayHeader_czech(int year, int month, int day, string expected) =>
        Assert.Equal(expected, DisplayFormat.DayHeader(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2026, 10, 26, "26. 10. – 1. 11. 2026")]
    [InlineData(2026, 12, 28, "28. 12. 2026 – 3. 1. 2027")]
    public void WeekLabel_same_and_cross_year(int year, int month, int day, string expected) =>
        Assert.Equal(expected, DisplayFormat.WeekLabel(new Week(new DateOnly(year, month, day))));

    [Fact]
    public void PlayerUrl_escapes_id_and_appends_week()
    {
        Assert.Equal("/players/a%2Fb%3Fc", DisplayFormat.PlayerUrl("a/b?c"));
        Assert.Equal("/players/abc?week=2026-10-26", DisplayFormat.PlayerUrl("abc", new Week(new DateOnly(2026, 10, 26))));
    }
}
