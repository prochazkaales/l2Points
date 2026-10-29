using L2Points.Web.Components.Layout;
using MudBlazor;

namespace L2Points.Web.Tests.Theme;

/// <summary>Keeps the MudTheme palettes in AppTheme in sync with the --l2p-* tokens in wwwroot/app.css.</summary>
public class AppThemeTokensTests
{
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Palette_mirrors_css_tokens(string theme)
    {
        var tokens = CssTokens.Read(theme);
        Palette palette = theme == "dark" ? AppTheme.Theme.PaletteDark : AppTheme.Theme.PaletteLight;

        (string Name, MudBlazor.Utilities.MudColor Color, string Token)[] pairs =
        [
            ("Primary", palette.Primary, "--l2p-primary"),
            ("PrimaryContrastText", palette.PrimaryContrastText, "--l2p-on-primary"),
            ("Secondary", palette.Secondary, "--l2p-secondary"),
            ("Background", palette.Background, "--l2p-bg"),
            ("Surface", palette.Surface, "--l2p-surface-solid"),
            ("TextPrimary", palette.TextPrimary, "--l2p-text"),
            ("TextSecondary", palette.TextSecondary, "--l2p-text-2"),
            ("TextDisabled", palette.TextDisabled, "--l2p-text-disabled"),
            ("ActionDisabled", palette.ActionDisabled, "--l2p-text-disabled"),
            ("Info", palette.Info, "--l2p-info"),
            ("Success", palette.Success, "--l2p-success"),
            ("Warning", palette.Warning, "--l2p-warning"),
            ("Error", palette.Error, "--l2p-danger"),
        ];

        foreach (var (name, color, token) in pairs)
        {
            var expected = CssTokens.Color(tokens[token]);
            Assert.True(
                (color.R, color.G, color.B) == ((byte)expected.R, (byte)expected.G, (byte)expected.B),
                $"[{theme}] {name} is #{color.R:X2}{color.G:X2}{color.B:X2} but {token} is {tokens[token]}.");
        }
    }

    [Fact]
    public void Theme_layout_and_buttons()
    {
        Assert.Equal("8px", AppTheme.Theme.LayoutProperties.DefaultBorderRadius);
        Assert.Equal("none", AppTheme.Theme.Typography.Button.TextTransform);
    }
}
