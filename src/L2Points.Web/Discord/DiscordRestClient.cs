using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace L2Points.Web.Discord;

/// <summary>
/// Calls to the Discord REST API and the attachment CDN. Request URLs carry the interaction token, so this client is
/// registered without HTTP logging (see <see cref="DiscordSetup"/>).
/// </summary>
public sealed class DiscordRestClient(HttpClient http, IOptions<DiscordOptions> options)
{
    public static readonly Uri ApiBaseAddress = new("https://discord.com/api/v10/");
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The <c>/report</c> guild command. <c>default_member_permissions: "0"</c> hides it from everyone but server admins,
    /// who then allow roles and channels in the server's integration settings.
    /// </summary>
    private const string CommandsJson =
        """[{"name":"report","description":"Zapsat účast party ze screenu","type":1,"default_member_permissions":"0","contexts":[0],"options":[{"type":11,"name":"screen","description":"Screen rozbalené party (Party Members)","required":true}]}]""";

    private const int ReadChunkBytes = 81920;

    private DiscordOptions Settings => options.Value;

    /// <summary>Replaces the message the interaction answered (the deferred reply or the panel with the clicked component).</summary>
    public async Task EditOriginalAsync(string interactionToken, DiscordMessage message, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync(
            $"webhooks/{Settings.ApplicationId}/{Uri.EscapeDataString(interactionToken)}/messages/@original",
            DiscordViews.ForEdit(message),
            DiscordJson.Options,
            ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendFollowupAsync(string interactionToken, DiscordMessage message, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"webhooks/{Settings.ApplicationId}/{Uri.EscapeDataString(interactionToken)}",
            message,
            DiscordJson.Options,
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Bulk overwrite of the guild commands, so registering again is harmless.</summary>
    public async Task RegisterCommandsAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"applications/{Settings.ApplicationId}/guilds/{Settings.GuildId}/commands")
        {
            Content = new StringContent(CommandsJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", Settings.BotToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Downloads a screenshot from the Discord CDN. The policy is checked again against the real response, and reading
    /// stops past <see cref="AttachmentPolicy.MaxBytes"/> whatever size the response declares.
    /// </summary>
    public async Task<byte[]> DownloadAttachmentAsync(string url, CancellationToken ct)
    {
        if (!AttachmentPolicy.IsTrustedUrl(url))
            throw new InvalidOperationException("The attachment is not on the Discord CDN.");

        // HttpClient.Timeout covers only the headers when the body is streamed; this covers the whole download.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();

        var headers = response.Content.Headers;
        var check = AttachmentPolicy.Check(url, headers.ContentType?.MediaType, headers.ContentLength ?? 0);
        if (check != AttachmentCheck.Ok)
            throw new InvalidOperationException($"The attachment was rejected: {check}.");

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var image = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
        {
            if (image.Length + read > AttachmentPolicy.MaxBytes)
                throw new InvalidOperationException("The attachment is larger than allowed.");

            image.Write(chunk, 0, read);
        }

        return image.ToArray();
    }
}
