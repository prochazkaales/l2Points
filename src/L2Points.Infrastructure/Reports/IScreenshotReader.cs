namespace L2Points.Infrastructure.Reports;

/// <param name="PanelFound">Whether a party member panel was recognised in the image.</param>
/// <param name="Nicks">Nicks read from the panel, in order.</param>
public sealed record ScreenshotReadResult(bool PanelFound, IReadOnlyList<string> Nicks);

/// <summary>The screenshot could not be processed (service failure, unreadable response).</summary>
public sealed class ScreenshotReadException : Exception
{
    public ScreenshotReadException(string message) : base(message)
    {
    }

    public ScreenshotReadException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>Reads the party member nicks from a game screenshot.</summary>
public interface IScreenshotReader
{
    /// <param name="knownNicks">Nicks of the players the result will be matched to; a hint for ambiguous characters.</param>
    Task<ScreenshotReadResult> ReadPartyMembersAsync(
        byte[] image, string mediaType, IReadOnlyList<string> knownNicks, CancellationToken ct = default);
}
