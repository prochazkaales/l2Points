using System.Text.Json;
using System.Text.Json.Serialization;

namespace L2Points.Web.Discord;

/// <summary>JSON settings shared by everything that reads or writes Discord payloads.</summary>
public static class DiscordJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public static class DiscordInteractionType
{
    public const int Ping = 1;
    public const int ApplicationCommand = 2;
    public const int MessageComponent = 3;
}

public static class DiscordResponseType
{
    public const int Pong = 1;
    public const int ChannelMessage = 4;
    public const int DeferredChannelMessage = 5;
    public const int DeferredUpdateMessage = 6;
    public const int UpdateMessage = 7;
}

// Incoming interaction: only the parts the bot reads.

public sealed record DiscordInteraction(
    string? Id,
    int Type,
    string? Token,
    string? GuildId,
    string? ChannelId,
    DiscordMember? Member,
    DiscordInteractionData? Data);

public sealed record DiscordMember(DiscordUser? User);

public sealed record DiscordUser(string? Id, string? Username, string? GlobalName);

public sealed record DiscordInteractionData(
    string? Name,
    IReadOnlyList<DiscordCommandOption>? Options,
    DiscordResolved? Resolved,
    string? CustomId,
    IReadOnlyList<string>? Values);

/// <summary>Option value is a string for attachment options (the attachment id), but may be a number or boolean for others.</summary>
public sealed record DiscordCommandOption(string? Name, JsonElement? Value);

public sealed record DiscordResolved(IReadOnlyDictionary<string, DiscordAttachment>? Attachments);

public sealed record DiscordAttachment(string? Url, string? ContentType, long Size);

// Outgoing response.

public sealed record DiscordInteractionResponse(int Type, DiscordMessage? Data = null);

public sealed record DiscordMessage(
    string? Content = null,
    int? Flags = null,
    IReadOnlyList<DiscordComponent>? Components = null,
    DiscordAllowedMentions? AllowedMentions = null)
{
    /// <summary>Flag value: only the invoking user sees the message.</summary>
    public const int Ephemeral = 64;
}

/// <summary>Empty <c>parse</c> list: nothing in the content can ping anyone.</summary>
public sealed record DiscordAllowedMentions(IReadOnlyList<string> Parse)
{
    public static DiscordAllowedMentions None { get; } = new([]);
}

/// <summary>Action row (type 1), button (2) or string select (3); members not used by the type stay null and are omitted.</summary>
public sealed record DiscordComponent(
    int Type,
    int? Style = null,
    string? Label = null,
    string? CustomId = null,
    bool? Disabled = null,
    string? Placeholder = null,
    int? MinValues = null,
    int? MaxValues = null,
    IReadOnlyList<DiscordSelectOption>? Options = null,
    IReadOnlyList<DiscordComponent>? Components = null)
{
    public const int ActionRowType = 1;
    public const int ButtonType = 2;
    public const int StringSelectType = 3;
}

public sealed record DiscordSelectOption(string Label, string Value, bool Default);
