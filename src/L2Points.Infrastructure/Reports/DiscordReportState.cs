using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Services;

namespace L2Points.Infrastructure.Reports;

/// <summary>A player matched to a nick read from the screenshot. <c>Exact</c> matches are always confirmed, <c>Similar</c> start unconfirmed.</summary>
public sealed record ReportPlayer(string UserId, string Nick, string ReadNick, NickMatchKind Kind, bool Confirmed);

/// <summary>Why one player's attendance was not recorded.</summary>
public sealed record ReportPlayerIssue(string Nick, AttendanceChangeResult Reason);

/// <summary>Outcome of a submit for one event.</summary>
public sealed record ReportEventResult(
    AttendanceEntry Event,
    string ActivityName,
    int Added,
    int AlreadyHad,
    IReadOnlyList<ReportPlayerIssue> Issues);

public sealed record ReportResult(IReadOnlyList<ReportEventResult> Events);

/// <summary>Everything the report holds besides its lifecycle columns. Every member has a default so that <c>{}</c> deserializes.</summary>
public sealed record DiscordReportState
{
    public bool PanelFound { get; init; }

    public IReadOnlyList<string> ReadNicks { get; init; } = [];

    public IReadOnlyList<ReportPlayer> Players { get; init; } = [];

    public IReadOnlyList<string> UnknownNicks { get; init; } = [];

    public DateOnly? Date { get; init; }

    public IReadOnlyList<AttendanceEntry> SelectedEvents { get; init; } = [];

    public ReportResult? Result { get; init; }
}
