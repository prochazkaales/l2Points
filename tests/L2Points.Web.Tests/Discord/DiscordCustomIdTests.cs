using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Web.Discord;

namespace L2Points.Web.Tests.Discord;

public class DiscordCustomIdTests
{
    [Fact]
    public void Format_and_parse_round_trip()
    {
        var id = Guid.NewGuid();

        var text = DiscordCustomId.Format(id, DiscordCustomId.Submit);

        Assert.Equal($"r:{id:N}:submit", text);
        Assert.True(DiscordCustomId.TryParse(text, out var parsedId, out var action));
        Assert.Equal(id, parsedId);
        Assert.Equal(DiscordCustomId.Submit, action);
    }

    [Theory]
    [InlineData("x:y")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("r:notaguid:submit")]
    [InlineData("x:00000000000000000000000000000001:submit")]
    [InlineData("r:00000000000000000000000000000001:")]
    [InlineData("r:00000000000000000000000000000001:submit:extra")]
    public void Malformed_custom_id_is_rejected(string? value) =>
        Assert.False(DiscordCustomId.TryParse(value, out _, out _));

    [Fact]
    public void Event_value_round_trips()
    {
        var entry = new AttendanceEntry(7, new DateOnly(2026, 10, 30), EventSlot.Morning);

        var value = DiscordCustomId.EventValue(entry);

        Assert.Equal("7|2026-10-30|2", value);
        Assert.True(DiscordCustomId.TryParseEventValue(value, out var parsed));
        Assert.Equal(entry, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7|2026-10-30")]
    [InlineData("x|2026-10-30|0")]
    [InlineData("7|30.10.2026|0")]
    [InlineData("7|2026-10-30|9")]
    public void Malformed_event_value_is_rejected(string value) =>
        Assert.False(DiscordCustomId.TryParseEventValue(value, out _));
}
