using L2Points.Web.Discord;

namespace L2Points.Web.Tests.Discord;

public class AttachmentPolicyTests
{
    private const string CdnPng = "https://cdn.discordapp.com/attachments/1/2/party.png?ex=1";

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/PNG; charset=binary")]
    public void Png_on_cdn_is_ok(string contentType) =>
        Assert.Equal(AttachmentCheck.Ok, AttachmentPolicy.Check(CdnPng, contentType, 1234));

    [Fact]
    public void Media_proxy_host_is_ok() =>
        Assert.Equal(AttachmentCheck.Ok, AttachmentPolicy.Check("https://media.discordapp.net/attachments/1/2/p.png", "image/png", 1));

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("")]
    [InlineData(null)]
    public void Pdf_is_not_image(string? contentType) =>
        Assert.Equal(AttachmentCheck.NotImage, AttachmentPolicy.Check(CdnPng, contentType, 1234));

    [Fact]
    public void Over_10_MB_is_too_large()
    {
        Assert.Equal(AttachmentCheck.TooLarge, AttachmentPolicy.Check(CdnPng, "image/png", AttachmentPolicy.MaxBytes + 1));
        Assert.Equal(AttachmentCheck.Ok, AttachmentPolicy.Check(CdnPng, "image/png", AttachmentPolicy.MaxBytes));
    }

    [Theory]
    [InlineData("https://evil.example/party.png")]
    [InlineData("https://cdn.discordapp.com.evil.example/party.png")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void Other_host_is_untrusted(string? url) =>
        Assert.Equal(AttachmentCheck.UntrustedHost, AttachmentPolicy.Check(url, "image/png", 1234));

    [Fact]
    public void Http_is_untrusted() =>
        Assert.Equal(AttachmentCheck.UntrustedHost, AttachmentPolicy.Check("http://cdn.discordapp.com/a/party.png", "image/png", 1234));
}
