using L2Points.Infrastructure.Reports;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Tests;

public class ClaudeScreenshotReaderTests
{
    [Fact]
    public void Parse_valid_response()
    {
        var result = ClaudeScreenshotReader.ParseResponse("""{"panelFound":true,"nicks":["Hubkins1010"," SemteX ",""]}""");

        Assert.True(result.PanelFound);
        Assert.Equal(["Hubkins1010", "SemteX"], result.Nicks);
    }

    [Fact]
    public void Parse_panel_not_found()
    {
        var result = ClaudeScreenshotReader.ParseResponse("""{"panelFound":false,"nicks":[]}""");

        Assert.False(result.PanelFound);
        Assert.Empty(result.Nicks);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    public void Parse_invalid_json_throws(string json)
    {
        Assert.Throws<ScreenshotReadException>(() => ClaudeScreenshotReader.ParseResponse(json));
    }

    [Theory]
    [InlineData("""{"panelFound":true}""")]
    [InlineData("""{"nicks":["A"]}""")]
    [InlineData("""{"panelFound":true,"nicks":"A"}""")]
    [InlineData("""{"panelFound":true,"nicks":[1]}""")]
    public void Parse_missing_nicks_throws(string json)
    {
        Assert.Throws<ScreenshotReadException>(() => ClaudeScreenshotReader.ParseResponse(json));
    }

    [Fact]
    public void Default_model_is_haiku()
    {
        Assert.Equal("claude-haiku-5-5", new AnthropicOptions().Model);
        Assert.Equal("Anthropic", AnthropicOptions.SectionName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Read_without_api_key_throws(string? apiKey)
    {
        var reader = CreateReader(apiKey);

        await Assert.ThrowsAsync<ScreenshotReadException>(() =>
            reader.ReadPartyMembersAsync([1, 2, 3], "image/png", ["A"]));
    }

    [Fact]
    public async Task Read_with_unsupported_media_type_throws_before_any_request()
    {
        var reader = CreateReader("test-key");

        await Assert.ThrowsAsync<ScreenshotReadException>(() =>
            reader.ReadPartyMembersAsync([1, 2, 3], "application/pdf", ["A"]));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, "image/webp", "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 }, "image/png", "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/png", "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, "image/png", "image/webp")]
    public void Detected_image_type_wins_over_declared(byte[] image, string declared, string expected) =>
        Assert.Equal(expected, ClaudeScreenshotReader.DetectMediaType(image, declared));

    [Fact]
    public void Unknown_bytes_keep_declared_type() =>
        Assert.Equal("image/png", ClaudeScreenshotReader.DetectMediaType([1, 2, 3], "image/png"));

    private static ClaudeScreenshotReader CreateReader(string? apiKey) =>
        new(Options.Create(new AnthropicOptions { ApiKey = apiKey }));
}
