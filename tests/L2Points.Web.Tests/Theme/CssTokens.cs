using System.Globalization;
using System.Text.RegularExpressions;

namespace L2Points.Web.Tests.Theme;

/// <summary>A colour with straight (non-premultiplied) alpha; R, G, B are 0..255.</summary>
public readonly record struct Rgba(double R, double G, double B, double A);

/// <summary>Reads the --l2p-* colour tokens from wwwroot/app.css and does the colour maths the contrast tests need.</summary>
public static partial class CssTokens
{
    /// <summary>
    /// Merges every rule of app.css whose selector list contains <c>:root[data-theme="theme"]</c> (a bare
    /// <c>:root</c> in the same list is the dark default) and returns its <c>--l2p-*</c> declarations.
    /// Later declarations win, as in the cascade.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Read(string theme)
    {
        var css = CommentPattern().Replace(File.ReadAllText(CssPath()), string.Empty);
        var selectorMarker = $":root[data-theme=\"{theme}\"]";
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match rule in RulePattern().Matches(css))
        {
            var selectors = rule.Groups["selector"].Value.Split(',').Select(s => s.Trim());
            if (!selectors.Contains(selectorMarker, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (Match declaration in DeclarationPattern().Matches(rule.Groups["body"].Value))
            {
                tokens[declaration.Groups["name"].Value] = declaration.Groups["value"].Value.Trim();
            }
        }

        return tokens;
    }

    /// <summary>Parses <c>#RRGGBB</c> and <c>rgba(r, g, b, a)</c>.</summary>
    public static Rgba Color(string value)
    {
        value = value.Trim();

        if (HexPattern().Match(value) is { Success: true } hex)
        {
            var digits = hex.Groups["hex"].Value;
            return new Rgba(
                int.Parse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                1);
        }

        if (RgbaPattern().Match(value) is { Success: true } rgba)
        {
            double Part(string name) => double.Parse(rgba.Groups[name].Value, CultureInfo.InvariantCulture);
            return new Rgba(Part("r"), Part("g"), Part("b"), Part("a"));
        }

        throw new FormatException($"Not a #RRGGBB or rgba() colour: '{value}'.");
    }

    /// <summary>Source-over compositing: <paramref name="top"/> painted on <paramref name="bottom"/>.</summary>
    public static Rgba Over(Rgba top, Rgba bottom)
    {
        var alpha = top.A + bottom.A * (1 - top.A);
        if (alpha <= 0)
        {
            return new Rgba(0, 0, 0, 0);
        }

        double Mix(double t, double b) => (t * top.A + b * bottom.A * (1 - top.A)) / alpha;
        return new Rgba(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B), alpha);
    }

    /// <summary><c>color-mix(in srgb, color share, transparent)</c>: the colour at <paramref name="share"/> of its alpha.</summary>
    public static Rgba Tint(Rgba color, double share) => color with { A = color.A * share };

    /// <summary>WCAG contrast ratio of opaque <paramref name="fg"/> over opaque <paramref name="bg"/>.</summary>
    public static double Ratio(Rgba fg, Rgba bg)
    {
        var a = Luminance(fg);
        var b = Luminance(bg);
        var (light, dark) = a >= b ? (a, b) : (b, a);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(Rgba c)
    {
        static double Channel(double v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static string CssPath() =>
        Path.Combine(FindRepoRoot(), "src", "L2Points.Web", "wwwroot", "app.css");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "L2Points.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (L2Points.slnx) not found.");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"(?<name>--l2p-[\w-]+)\s*:\s*(?<value>[^;]+);")]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(@"^#(?<hex>[0-9A-Fa-f]{6})$")]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^rgba\(\s*(?<r>[\d.]+)\s*,\s*(?<g>[\d.]+)\s*,\s*(?<b>[\d.]+)\s*,\s*(?<a>[\d.]+)\s*\)$")]
    private static partial Regex RgbaPattern();
}
