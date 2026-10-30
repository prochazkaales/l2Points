using L2Points.Infrastructure.Reports;

namespace L2Points.Infrastructure.Tests;

/// <summary>Screenshot reader returning configured nicks and recording what it was asked.</summary>
internal sealed class FakeScreenshotReader : IScreenshotReader
{
    public bool PanelFound { get; set; } = true;

    public IReadOnlyList<string> Nicks { get; set; } = [];

    public Exception? Failure { get; set; }

    public IReadOnlyList<string> KnownNicks { get; private set; } = [];

    public Task<ScreenshotReadResult> ReadPartyMembersAsync(
        byte[] image, string mediaType, IReadOnlyList<string> knownNicks, CancellationToken ct = default)
    {
        KnownNicks = knownNicks;
        if (Failure is not null)
            throw Failure;
        return Task.FromResult(new ScreenshotReadResult(PanelFound, Nicks));
    }
}
