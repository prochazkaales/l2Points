using MudBlazor;

namespace L2Points.Web.Components.Layout;

public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#6d4ae0",
            Secondary = "#c2410c",
            Tertiary = "#0e7490",
            AppbarBackground = "#4c2fb8",
            AppbarText = "#ffffff",
            Background = "#f4f3f9",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#1f1b2e",
            DrawerIcon = "#5b5675",
            TextPrimary = "#1f1b2e",
            TextSecondary = "#5b5675",
            ActionDefault = "#5b5675",
            LinesDefault = "#e3e0ee",
            TableLines = "#e3e0ee",
            Divider = "#e3e0ee",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#9d7bff",
            Secondary = "#ff9a3c",
            Tertiary = "#34d1e5",
            AppbarBackground = "#1a1726",
            AppbarText = "#ece9f7",
            Background = "#12101c",
            Surface = "#1c1929",
            DrawerBackground = "#171423",
            DrawerText = "#ece9f7",
            DrawerIcon = "#a8a3c2",
            TextPrimary = "#ece9f7",
            TextSecondary = "#a8a3c2",
            ActionDefault = "#a8a3c2",
            LinesDefault = "#2e2a42",
            TableLines = "#2e2a42",
            Divider = "#2e2a42",
        },
    };
}
