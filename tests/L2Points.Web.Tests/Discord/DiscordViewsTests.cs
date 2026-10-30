using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Reports;
using L2Points.Web.Discord;

namespace L2Points.Web.Tests.Discord;

public class DiscordViewsTests
{
    private static readonly DateOnly Day = new(2026, 10, 30);

    private static readonly DiscordReportState WithResult = new()
    {
        PanelFound = true,
        Players = [new ReportPlayer("u-1", "Bob", "Bob", NickMatchKind.Exact, true)],
        Date = Day,
        Result = new ReportResult([new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", 1, 0, [])]),
    };

    private static DiscordReportView View(
        DiscordReportOutcome outcome, DiscordReportStatus status, DiscordReportState? state = null) =>
        new(Guid.NewGuid(), outcome, status, "Alice", state ?? new DiscordReportState { PanelFound = true }, [Day], []);

    [Theory]
    [InlineData(DiscordReportOutcome.Ok, DiscordReportStatus.Processing)]
    [InlineData(DiscordReportOutcome.Ok, DiscordReportStatus.Submitting)]
    public void Work_in_progress_leaves_the_message(DiscordReportOutcome outcome, DiscordReportStatus status) =>
        Assert.Null(DiscordViews.ToMessage(View(outcome, status)));

    [Fact]
    public void Pending_shows_the_panel()
    {
        var view = View(DiscordReportOutcome.Ok, DiscordReportStatus.Pending);

        Assert.Equal(DiscordMessages.ReportPanel(view), DiscordViews.ToMessage(view), MessageComparer.Instance);
    }

    [Theory]
    [InlineData(DiscordReportOutcome.Ok)]
    [InlineData(DiscordReportOutcome.Finished)]
    public void Completed_shows_the_result(DiscordReportOutcome outcome)
    {
        var message = DiscordViews.ToMessage(View(outcome, DiscordReportStatus.Completed, WithResult));

        Assert.StartsWith("Zapsáno.", message!.Content);
    }

    [Theory]
    [InlineData(DiscordReportOutcome.NotFound, DiscordReportStatus.Failed, true, DiscordMessages.Texts.Closed)]
    [InlineData(DiscordReportOutcome.NotOwner, DiscordReportStatus.Failed, true, DiscordMessages.Texts.NotOwner)]
    [InlineData(DiscordReportOutcome.Expired, DiscordReportStatus.Expired, true, DiscordMessages.Texts.Expired)]
    [InlineData(DiscordReportOutcome.Finished, DiscordReportStatus.Expired, true, DiscordMessages.Texts.Expired)]
    [InlineData(DiscordReportOutcome.Finished, DiscordReportStatus.Cancelled, true, DiscordMessages.Texts.Cancelled)]
    [InlineData(DiscordReportOutcome.Ok, DiscordReportStatus.Cancelled, true, DiscordMessages.Texts.Cancelled)]
    [InlineData(DiscordReportOutcome.Finished, DiscordReportStatus.Failed, false, DiscordMessages.Texts.PanelNotFound)]
    [InlineData(DiscordReportOutcome.Ok, DiscordReportStatus.Failed, false, DiscordMessages.Texts.PanelNotFound)]
    [InlineData(DiscordReportOutcome.Finished, DiscordReportStatus.Failed, true, DiscordMessages.Texts.ProcessingFailed)]
    public void Other_states_show_a_notice(
        DiscordReportOutcome outcome, DiscordReportStatus status, bool panelFound, string expected)
    {
        var message = DiscordViews.ToMessage(View(outcome, status, new DiscordReportState { PanelFound = panelFound }));

        Assert.Equal(expected, message!.Content);
        Assert.Null(message.Components);
    }

    [Fact]
    public void Not_owner_never_shows_the_result()
    {
        var message = DiscordViews.ToMessage(View(DiscordReportOutcome.NotOwner, DiscordReportStatus.Completed, WithResult));

        Assert.Equal(DiscordMessages.Texts.NotOwner, message!.Content);
    }

    /// <summary>Messages hold lists, so record equality compares references; compare the serialized form instead.</summary>
    private sealed class MessageComparer : IEqualityComparer<DiscordMessage?>
    {
        public static MessageComparer Instance { get; } = new();

        public bool Equals(DiscordMessage? x, DiscordMessage? y) =>
            System.Text.Json.JsonSerializer.Serialize(x, DiscordJson.Options)
            == System.Text.Json.JsonSerializer.Serialize(y, DiscordJson.Options);

        public int GetHashCode(DiscordMessage? obj) => 0;
    }
}
