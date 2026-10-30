using L2Points.Infrastructure.Reports;

namespace L2Points.Infrastructure.Tests;

/// <summary>Screenshot reader returning configured nicks and recording what it was asked.</summary>
internal sealed class FakeScreenshotReader : IScreenshotReader
{
    public bool PanelFound { get; set; } = true;

    public IReadOnlyList<string> Nicks { get; set; } = [];

    public Exception? Failure { get; set; }

    public IReadOnlyList<string> KnownNicks { get; private set; } = [];

    /// <summary>Runs while the read is in progress, to simulate something happening to the report meanwhile.</summary>
    public Func<Task>? WhileReading { get; set; }

    public async Task<ScreenshotReadResult> ReadPartyMembersAsync(
        byte[] image, string mediaType, IReadOnlyList<string> knownNicks, CancellationToken ct = default)
    {
        KnownNicks = knownNicks;
        if (Failure is not null)
            throw Failure;
        if (WhileReading is not null)
            await WhileReading();
        return new ScreenshotReadResult(PanelFound, Nicks);
    }
}
