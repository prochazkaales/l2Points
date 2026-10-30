using L2Points.Infrastructure.Reports;

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
}
