namespace L2Points.Web.Discord;

public enum AttachmentCheck
{
    Ok,
    NotImage,
    TooLarge,
    UntrustedHost,
}

/// <summary>Decides whether a screenshot attached to a command may be downloaded and sent to the reader.</summary>
public static class AttachmentPolicy
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] TrustedHosts = ["cdn.discordapp.com", "media.discordapp.net"];

    private static readonly string[] ImageTypes = ["image/png", "image/jpeg", "image/webp"];

    /// <summary>The host is checked first so that nothing outside the Discord CDN is ever fetched.</summary>
    public static AttachmentCheck Check(string? url, string? contentType, long size)
    {
        if (!IsTrustedUrl(url))
            return AttachmentCheck.UntrustedHost;

        var mediaType = contentType?.Split(';')[0].Trim();
        if (mediaType is null || !ImageTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            return AttachmentCheck.NotImage;

        return size > MaxBytes ? AttachmentCheck.TooLarge : AttachmentCheck.Ok;
    }

    /// <summary>Https on the Discord CDN; checked before anything is fetched.</summary>
    public static bool IsTrustedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && TrustedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
}
