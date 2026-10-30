namespace L2Points.Infrastructure.Reports;

/// <summary>Lifecycle of a report made from a Discord screenshot.</summary>
public enum DiscordReportStatus
{
    /// <summary>Created, the screenshot is being read.</summary>
    Processing = 0,

    /// <summary>Read and matched, waiting for the reporter's choices and submit.</summary>
    Pending = 1,

    /// <summary>Submit is in progress.</summary>
    Submitting = 2,

    Completed = 3,
    Cancelled = 4,
    Expired = 5,
    Failed = 6,
}

/// <summary>One report of party attendance posted from a Discord channel; the mutable part lives in <see cref="StateJson"/>.</summary>
public class DiscordReport
{
    public const int DiscordIdMaxLength = 32;
    public const int DiscordUserNameMaxLength = 100;

    public Guid Id { get; set; }

    public string DiscordUserId { get; set; } = string.Empty;

    public string DiscordUserName { get; set; } = string.Empty;

    public string GuildId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DiscordReportStatus Status { get; set; }

    /// <summary>Serialized <see cref="DiscordReportState"/>.</summary>
    public string StateJson { get; set; } = "{}";
}
