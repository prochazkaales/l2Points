using System.Threading.Channels;

namespace L2Points.Web.Discord;

/// <summary>Work that must not hold up the interaction response (Discord waits 3 s at most).</summary>
public abstract record DiscordJob;

/// <summary>Download the screenshot, read it and replace the deferred reply with the panel.</summary>
public sealed record AnalyzeJob(Guid ReportId, string InteractionToken, string AttachmentUrl, string ContentType) : DiscordJob
{
    // The interaction token lets anyone edit the reply, so it must never end up in a log.
    public override string ToString() => $"{nameof(AnalyzeJob)} {{ ReportId = {ReportId} }}";
}

/// <summary>Record the attendance of a report and post the summary.</summary>
public sealed record SubmitJob(Guid ReportId, string DiscordUserId, string InteractionToken) : DiscordJob
{
    public override string ToString() => $"{nameof(SubmitJob)} {{ ReportId = {ReportId} }}";
}

/// <summary>In-process queue between the interactions endpoint and <see cref="DiscordJobWorker"/>.</summary>
public sealed class DiscordJobQueue
{
    private readonly Channel<DiscordJob> _channel =
        Channel.CreateUnbounded<DiscordJob>(new UnboundedChannelOptions { SingleReader = true });

    public bool TryEnqueue(DiscordJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<DiscordJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
