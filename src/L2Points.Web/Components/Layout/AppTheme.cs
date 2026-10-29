using MudBlazor;

namespace L2Points.Web.Components.Layout;

public static class AppTheme
{
    // Self-hosted Outfit (wwwroot/fonts, declared in app.css) with the system font stack as fallback; no external font CDN.
    private static readonly string[] FontFamily = ["Outfit", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontFamily },
            H1 = new H1Typography { FontFamily = FontFamily },
            H2 = new H2Typography { FontFamily = FontFamily },
            H3 = new H3Typography { FontFamily = FontFamily },
            H4 = new H4Typography { FontFamily = FontFamily },
            H5 = new H5Typography { FontFamily = FontFamily },
            H6 = new H6Typography { FontFamily = FontFamily },
            Subtitle1 = new Subtitle1Typography { FontFamily = FontFamily },
            Subtitle2 = new Subtitle2Typography { FontFamily = FontFamily },
            Body1 = new Body1Typography { FontFamily = FontFamily },
            Body2 = new Body2Typography { FontFamily = FontFamily },
            Button = new ButtonTypography { FontFamily = FontFamily, TextTransform = "none", FontWeight = "600" },
            Caption = new CaptionTypography { FontFamily = FontFamily },
            Overline = new OverlineTypography { FontFamily = FontFamily },
        },
        // The palettes mirror the --l2p-* tokens in wwwroot/app.css; AppThemeTokensTests keeps them in sync.
        PaletteLight = new PaletteLight
        {
            Primary = "#6D4AE0",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#A93A0B",
            Tertiary = "#0e7490",
            AppbarBackground = "#F8F8FD",
            AppbarText = "#1D1838",
            Background = "#E8EAF6",
            Surface = "#F8F8FD",
            DrawerBackground = "#F8F8FD",
            DrawerText = "#1D1838",
            DrawerIcon = "#4F4A6E",
            TextPrimary = "#1D1838",
            TextSecondary = "#4F4A6E",
            TextDisabled = "#77738F",
            ActionDefault = "#4F4A6E",
            LinesDefault = "#DDDAEE",
            TableLines = "#DDDAEE",
            Divider = "#DDDAEE",
            Success = "#086E59",
            Warning = "#7A5200",
            Info = "#0B5CAD",
            Error = "#A3241A",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8B6CFF",
            PrimaryContrastText = "#0B0A1A",
            Secondary = "#FF9A3C",
            Tertiary = "#34d1e5",
            AppbarBackground = "#16132A",
            AppbarText = "#F4F2FF",
            Background = "#0B0A1A",
            Surface = "#16132A",
            DrawerBackground = "#16132A",
            DrawerText = "#F4F2FF",
            DrawerIcon = "#B9B5D6",
            TextPrimary = "#F4F2FF",
            TextSecondary = "#B9B5D6",
            TextDisabled = "#8C88AE",
            ActionDefault = "#B9B5D6",
            LinesDefault = "#2A2544",
            TableLines = "#2A2544",
            Divider = "#2A2544",
            Success = "#6FE3C8",
            Warning = "#F5C15C",
            Info = "#7CC4FF",
            Error = "#FF8A8A",
        },
    };
}
