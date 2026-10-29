using MudBlazor;

namespace L2Points.Web.Components.Layout;

public static class AppTheme
{
    // System font stack (no external font CDN); Roboto is used when installed locally.
    private static readonly string[] FontFamily = ["Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
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
            Button = new ButtonTypography { FontFamily = FontFamily },
            Caption = new CaptionTypography { FontFamily = FontFamily },
            Overline = new OverlineTypography { FontFamily = FontFamily },
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#6D4AE0",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#c2410c",
            Tertiary = "#0e7490",
            AppbarBackground = "#F8F8FD",
            AppbarText = "#1D1838",
            Background = "#EEF0FA",
            Surface = "#F8F8FD",
            DrawerBackground = "#F8F8FD",
            DrawerText = "#1D1838",
            DrawerIcon = "#4F4A6E",
            TextPrimary = "#1D1838",
            TextSecondary = "#4F4A6E",
            ActionDefault = "#4F4A6E",
            LinesDefault = "#DDDAEE",
            TableLines = "#DDDAEE",
            Divider = "#DDDAEE",
            Success = "#0B7A63",
            Warning = "#946300",
            Error = "#C0392B",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8B6CFF",
            PrimaryContrastText = "#0B0A1A",
            Secondary = "#ff9a3c",
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
            ActionDefault = "#B9B5D6",
            LinesDefault = "#2A2544",
            TableLines = "#2A2544",
            Divider = "#2A2544",
            Success = "#6FE3C8",
            Warning = "#F5C15C",
            Error = "#FF8A8A",
        },
    };
}
