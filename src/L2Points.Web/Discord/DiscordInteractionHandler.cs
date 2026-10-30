using System.Globalization;
using System.Text.Json;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Reports;
using Microsoft.Extensions.Options;
using static L2Points.Web.Discord.DiscordMessages;

namespace L2Points.Web.Discord;

/// <summary>Status code and JSON body (serialized with <see cref="DiscordJson.Options"/>) of the endpoint's response.</summary>
public sealed record DiscordHandlerResult(int StatusCode, object? Body);

/// <summary>
/// Answers Discord interactions within Discord's 3 s limit: verifies the request, applies the reporter's choices
/// and hands the slow work (reading the screenshot, submitting) to <see cref="DiscordJobQueue"/>.
/// </summary>
public sealed class DiscordInteractionHandler(
    IOptions<DiscordOptions> discordOptions,
    IOptions<AnthropicOptions> anthropicOptions,
    IDiscordReportService reports,
    DiscordJobQueue queue,
    TimeProvider time)
{
    public const string ReportCommandName = "report";

    private static readonly DiscordHandlerResult NotFound = new(StatusCodes.Status404NotFound, null);
    private static readonly DiscordHandlerResult Unauthorized = new(StatusCodes.Status401Unauthorized, null);
    private static readonly DiscordHandlerResult BadRequest = new(StatusCodes.Status400BadRequest, null);

    public async Task<DiscordHandlerResult> HandleAsync(byte[] body, string? signature, string? timestamp, CancellationToken ct)
    {
        var discord = discordOptions.Value;
        if (!DiscordOptions.IsEnabled(discord, anthropicOptions.Value))
            return NotFound;

        if (!DiscordSignature.Verify(discord.PublicKey!, signature, timestamp, body, time.GetUtcNow()))
            return Unauthorized;

        DiscordInteraction? interaction;
        try
        {
            interaction = JsonSerializer.Deserialize<DiscordInteraction>(body, DiscordJson.Options);
        }
        catch (JsonException)
        {
            return BadRequest;
        }

        if (interaction is null)
            return BadRequest;

        if (interaction.Type == DiscordInteractionType.Ping)
            return Respond(DiscordResponseType.Pong);

        // Missing guild = direct message; only the party's own server may report.
        if (interaction.GuildId is null || interaction.GuildId != discord.GuildId)
            return Respond(DiscordResponseType.ChannelMessage, Notice(Texts.WrongGuild));

        if (interaction.Member?.User is not { Id: { } userId } user || string.IsNullOrEmpty(interaction.Token))
            return BadRequest;

        return interaction.Type switch
        {
            DiscordInteractionType.ApplicationCommand when interaction.Data?.Name == ReportCommandName =>
                await StartReportAsync(interaction, user, userId, interaction.Token, ct),
            DiscordInteractionType.MessageComponent =>
                await HandleComponentAsync(interaction.Data, userId, interaction.Token, ct),
            _ => BadRequest,
        };
    }

    private async Task<DiscordHandlerResult> StartReportAsync(
        DiscordInteraction interaction, DiscordUser user, string userId, string token, CancellationToken ct)
    {
        var data = interaction.Data!;
        var attachmentId = data.Options?.FirstOrDefault()?.Value is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        DiscordAttachment? attachment = null;
        if (attachmentId is not null)
            data.Resolved?.Attachments?.TryGetValue(attachmentId, out attachment);

        if (attachment is null || AttachmentPolicy.Check(attachment.Url, attachment.ContentType, attachment.Size) != AttachmentCheck.Ok)
            return Respond(DiscordResponseType.ChannelMessage, Notice(Texts.BadAttachment));

        var reporter = new DiscordReporter(userId, user.GlobalName ?? user.Username ?? userId, interaction.GuildId!, interaction.ChannelId ?? "");
        var reportId = await reports.CreateAsync(reporter, ct);

        if (!queue.TryEnqueue(new AnalyzeJob(reportId, token, attachment.Url!, attachment.ContentType!)))
        {
            await reports.MarkFailedAsync(reportId, ct);
            return Respond(DiscordResponseType.ChannelMessage, Notice(Texts.ProcessingFailed));
        }

        return Respond(DiscordResponseType.DeferredChannelMessage, new DiscordMessage(Flags: DiscordMessage.Ephemeral));
    }

    private async Task<DiscordHandlerResult> HandleComponentAsync(
        DiscordInteractionData? data, string userId, string token, CancellationToken ct)
    {
        if (!DiscordCustomId.TryParse(data?.CustomId, out var reportId, out var action))
            return Respond(DiscordResponseType.ChannelMessage, Notice(Texts.Closed));

        var values = data!.Values ?? [];
        if (action == DiscordCustomId.Submit)
            return await QueueSubmitAsync(reportId, userId, token, ct);

        var view = action switch
        {
            DiscordCustomId.Date when TryParseDate(values, out var date) =>
                await reports.SelectDateAsync(reportId, userId, date, ct),
            DiscordCustomId.Events =>
                await reports.SelectEventsAsync(reportId, userId, ParseEvents(values), ct),
            DiscordCustomId.Similar =>
                await reports.ConfirmSimilarAsync(reportId, userId, values, ct),
            DiscordCustomId.Cancel =>
                await reports.CancelAsync(reportId, userId, ct),
            _ => null,
        };

        return view is null
            ? Respond(DiscordResponseType.ChannelMessage, Notice(Texts.Closed))
            : Update(view);
    }

    /// <summary>Submit only starts here; the worker records the attendance and edits the panel when it is done.</summary>
    private async Task<DiscordHandlerResult> QueueSubmitAsync(Guid reportId, string userId, string token, CancellationToken ct)
    {
        var view = await reports.GetAsync(reportId, userId, ct);
        if (view is { Outcome: DiscordReportOutcome.Ok, Status: DiscordReportStatus.Pending }
            && queue.TryEnqueue(new SubmitJob(reportId, userId, token)))
            return Respond(DiscordResponseType.DeferredUpdateMessage);

        return Update(view);
    }

    /// <summary>Replaces the panel with the message for the view; acknowledges without a change while work is in progress.</summary>
    private static DiscordHandlerResult Update(DiscordReportView view) =>
        DiscordViews.ToMessage(view) is { } message
            ? Respond(DiscordResponseType.UpdateMessage, DiscordViews.ForEdit(message))
            : Respond(DiscordResponseType.DeferredUpdateMessage);

    private static bool TryParseDate(IReadOnlyList<string> values, out DateOnly date)
    {
        date = default;
        return values is [var value]
            && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Values come from options the bot itself rendered; anything unparsable is ignored.</summary>
    private static List<AttendanceEntry> ParseEvents(IReadOnlyList<string> values) =>
        values.Select(v => DiscordCustomId.TryParseEventValue(v, out var e) ? (AttendanceEntry?)e : null)
            .OfType<AttendanceEntry>()
            .ToList();

    private static DiscordHandlerResult Respond(int type, DiscordMessage? message = null) =>
        new(StatusCodes.Status200OK, new DiscordInteractionResponse(type, message));
}
