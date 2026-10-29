using L2Points.Web.Formatting;

namespace L2Points.Web.Tests.Formatting;

public class TimeInputTests
{
    [Theory]
    [InlineData("19:30", 19, 30)]
    [InlineData("3:00", 3, 0)]
    [InlineData(" 01:30 ", 1, 30)]
    public void Parses_hours_and_minutes(string text, int hour, int minute)
    {
        Assert.True(TimeInput.TryParse(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("24:00")]
    [InlineData("19.30")]
    [InlineData("7")]
    [InlineData("ab:cd")]
    public void Rejects_invalid(string? text) =>
        Assert.False(TimeInput.TryParse(text, out _));

    [Fact]
    public void Formats_two_digit_hours() =>
        Assert.Equal("03:00", TimeInput.Format(new TimeOnly(3, 0)));
}
