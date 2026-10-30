using L2Points.Infrastructure.Reports;
using static L2Points.Web.Discord.DiscordMessages;

namespace L2Points.Web.Discord;

/// <summary>The one place that decides what the reporter sees for a report view.</summary>
public static class DiscordViews
{
    /// <summary>Message for the view, or null while the report is being read or submitted (the current message stays).</summary>
    public static DiscordMessage? ToMessage(DiscordReportView view) => view.Outcome switch
    {
        // NotFound and NotOwner views carry a placeholder status, so the outcome decides first.
        DiscordReportOutcome.NotFound => Notice(Texts.Closed),
        DiscordReportOutcome.NotOwner => Notice(Texts.NotOwner),
        DiscordReportOutcome.Expired => Notice(Texts.Expired),
        _ => view.Status switch
        {
            DiscordReportStatus.Processing or DiscordReportStatus.Submitting => null,
            DiscordReportStatus.Pending => ReportPanel(view),
            DiscordReportStatus.Completed => Result(view),
            DiscordReportStatus.Cancelled => Notice(Texts.Cancelled),
            DiscordReportStatus.Expired => Notice(Texts.Expired),
            DiscordReportStatus.Failed when !view.State.PanelFound => Notice(Texts.PanelNotFound),
            DiscordReportStatus.Failed => Notice(Texts.ProcessingFailed),
            _ => Notice(Texts.Closed),
        },
    };

    /// <summary>
    /// An edit keeps the visibility of the message it replaces (the reply is already ephemeral), so the flags are left
    /// out. Discord keeps every field an edit omits, so a message without controls sends an empty component list to
    /// remove the panel's selects and buttons.
    /// </summary>
    public static DiscordMessage ForEdit(DiscordMessage message) =>
        message with { Flags = null, Components = message.Components ?? [] };
}
