using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using L2Points.Web.Discord;
using Microsoft.Extensions.Options;

namespace L2Points.Web.Tests.Discord;

public class DiscordRestClientTests
{
    private const string AttachmentUrl = "https://cdn.discordapp.com/attachments/1/2/party.png";

    private readonly StubHandler _handler = new();

    private DiscordRestClient Client() =>
        new(
            new HttpClient(_handler) { BaseAddress = DiscordRestClient.ApiBaseAddress },
            Options.Create(new DiscordOptions { ApplicationId = "app-1", GuildId = "guild-1", BotToken = "bot-token" }));

    private static HttpResponseMessage Image(int bytes, string? finalUrl = null)
    {
        var content = new ByteArrayContent(new byte[bytes]);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
            RequestMessage = finalUrl is null ? null : new HttpRequestMessage(HttpMethod.Get, finalUrl),
        };
    }

    [Fact]
    public async Task Edit_sends_empty_components_for_a_notice()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await Client().EditOriginalAsync(
            "token", DiscordMessages.Notice(DiscordMessages.Texts.Cancelled), CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, _handler.Request!.Method);
        Assert.Equal("https://discord.com/api/v10/webhooks/app-1/token/messages/@original", _handler.Request.RequestUri!.AbsoluteUri);
        using var body = JsonDocument.Parse(_handler.Body!);
        Assert.Equal(0, body.RootElement.GetProperty("components").GetArrayLength());
        Assert.Equal(DiscordMessages.Texts.Cancelled, body.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Download_returns_the_image()
    {
        _handler.Respond = _ => Image(1234);

        var image = await Client().DownloadAttachmentAsync(AttachmentUrl, CancellationToken.None);

        Assert.Equal(1234, image.Length);
    }

    [Fact]
    public async Task Download_rejects_a_redirect_to_another_host()
    {
        _handler.Respond = _ => Image(10, finalUrl: "https://example.com/party.png");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client().DownloadAttachmentAsync(AttachmentUrl, CancellationToken.None));
    }

    [Fact]
    public async Task Download_stops_past_the_size_limit_whatever_the_declared_size()
    {
        _handler.Respond = _ =>
        {
            var response = Image((int)AttachmentPolicy.MaxBytes + 1);
            response.Content.Headers.ContentLength = 10;
            return response;
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client().DownloadAttachmentAsync(AttachmentUrl, CancellationToken.None));
    }

    [Fact]
    public async Task Download_never_requests_an_untrusted_url()
    {
        _handler.Respond = _ => Image(10);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client().DownloadAttachmentAsync("https://example.com/party.png", CancellationToken.None));
        Assert.Null(_handler.Request);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Respond(request);
        }
    }
}
