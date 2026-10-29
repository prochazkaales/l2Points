using static L2Points.Web.Tests.Theme.CssTokens;

namespace L2Points.Web.Tests.Theme;

/// <summary>
/// Contrast of the --l2p-* colour tokens, composed the way the browser paints them (see the "Kontrasty" section of
/// docs/superpowers/specs/2026-10-29-ui-ux-audit.md): page colour, the glow on top of it, the glass card on the glow,
/// the nested glass on the card and the "me" row tint on the card.
/// </summary>
public class TokenContrastTests
{
    private const double RowMeShare = 0.10;
    private const double BadgeFill = 0.16;

    private static readonly string[] TextTokens =
    [
        "--l2p-text", "--l2p-text-2", "--l2p-muted", "--l2p-primary-text",
        "--l2p-place-1", "--l2p-place-2", "--l2p-place-3",
        "--l2p-info", "--l2p-success", "--l2p-warning", "--l2p-danger", "--l2p-secondary",
    ];

    private static readonly string[] Categories = ["normal", "evening", "morning", "cs"];

    private sealed record Surfaces(Rgba Card, Rgba Nested, Rgba Me);

    private static Surfaces ComposeSurfaces(IReadOnlyDictionary<string, string> t)
    {
        var glow = Over(Color(t["--l2p-glow-1"]), Color(t["--l2p-bg"]));
        var card = Over(Color(t["--l2p-glass"]), glow);
        var nested = Over(Color(t["--l2p-glass-strong"]), card);
        var me = Over(Tint(Color(t["--l2p-primary"]), RowMeShare), card);
        return new Surfaces(card, nested, me);
    }

    private static Rgba Token(IReadOnlyDictionary<string, string> t, string name) =>
        t.TryGetValue(name, out var value)
            ? Color(value)
            : throw new Xunit.Sdk.XunitException($"Token {name} is not defined.");

    private static void AssertRatio(string theme, string token, string on, double actual, double minimum)
    {
        Assert.True(actual >= minimum,
            $"[{theme}] {token} on {on}: {actual:F2}:1, needs {minimum:F1}:1.");
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Text_tokens_reach_4_5_over_glass_and_glow(string theme)
    {
        var t = Read(theme);
        var s = ComposeSurfaces(t);

        foreach (var name in TextTokens)
        {
            var fg = Token(t, name);
            AssertRatio(theme, name, "card", Ratio(fg, s.Card), 4.5);
            AssertRatio(theme, name, "nested", Ratio(fg, s.Nested), 4.5);
            AssertRatio(theme, name, "me row", Ratio(fg, s.Me), 4.5);
        }
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Tone_badges_reach_4_5_on_their_fill(string theme)
    {
        var t = Read(theme);
        var s = ComposeSurfaces(t);

        foreach (var name in new[] { "--l2p-tone-high", "--l2p-tone-mid", "--l2p-tone-low" })
        {
            var tone = Token(t, name);
            AssertRatio(theme, name, "badge on card", Ratio(tone, Over(Tint(tone, BadgeFill), s.Card)), 4.5);
            AssertRatio(theme, name, "badge on me row", Ratio(tone, Over(Tint(tone, BadgeFill), s.Me)), 4.5);
        }
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Category_badges_reach_4_5_on_nested_glass(string theme)
    {
        var t = Read(theme);
        var s = ComposeSurfaces(t);

        foreach (var category in Categories)
        {
            var text = Token(t, $"--l2p-cat-{category}-text");
            var fill = Token(t, $"--l2p-cat-{category}");
            AssertRatio(theme, $"--l2p-cat-{category}-text", "badge on nested",
                Ratio(text, Over(Tint(fill, BadgeFill), s.Nested)), 4.5);
        }
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Disabled_text_reaches_3_on_glass(string theme)
    {
        var t = Read(theme);
        var s = ComposeSurfaces(t);

        AssertRatio(theme, "--l2p-text-disabled", "card", Ratio(Token(t, "--l2p-text-disabled"), s.Card), 3.0);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void Row_me_token_matches_the_tested_share(string theme)
    {
        var t = Read(theme);

        Assert.Equal("color-mix(in srgb, var(--l2p-primary) 10%, transparent)", t["--l2p-row-me"]);
    }
}
