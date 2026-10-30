using L2Points.Domain.Reports;
using L2Points.Domain.Stats;

namespace L2Points.Infrastructure.Reports;

public sealed record DiscordReporter(string DiscordUserId, string DiscordUserName, string GuildId, string ChannelId);

public enum DiscordReportOutcome
{
    Ok,
    NotFound,
    NotOwner,
    Expired,

    /// <summary>The report is completed, cancelled, expired or failed; the view holds the stored state.</summary>
    Finished,
}

/// <param name="EventOptions">Selectable events of <c>State.Date</c>; empty when no date is chosen.</param>
public sealed record DiscordReportView(
    Guid Id,
    DiscordReportOutcome Outcome,
    DiscordReportStatus Status,
    string ReporterName,
    DiscordReportState State,
    IReadOnlyList<DateOnly> DateOptions,
    IReadOnlyList<ReportEventOption> EventOptions);

/// <summary>Creates reports and holds the reporter's choices until the report is submitted.</summary>
public interface IDiscordReportService
{
    Task<Guid> CreateAsync(DiscordReporter reporter, CancellationToken ct = default);

    /// <summary>Reads the screenshot and matches players. Reader exceptions propagate; the caller then calls <see cref="MarkFailedAsync"/>.</summary>
    Task<DiscordReportView> AnalyzeAsync(Guid reportId, byte[] image, string mediaType, CancellationToken ct = default);

    Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct = default);

    Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct = default);

    Task<DiscordReportView> SelectDateAsync(Guid reportId, string discordUserId, DateOnly date, CancellationToken ct = default);

    Task<DiscordReportView> SelectEventsAsync(
        Guid reportId, string discordUserId, IReadOnlyList<AttendanceEntry> events, CancellationToken ct = default);

    Task<DiscordReportView> ConfirmSimilarAsync(
        Guid reportId, string discordUserId, IReadOnlyList<string> userIds, CancellationToken ct = default);

    Task<DiscordReportView> CancelAsync(Guid reportId, string discordUserId, CancellationToken ct = default);

    Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct = default);
}
