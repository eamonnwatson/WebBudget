using MudBlazor;

namespace PredictiveBudget.Web.Styling;

public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0f766e",
            Secondary = "#e0a82e",
            Tertiary = "#124b5b",
            Background = "#eef5f7",
            Surface = "#ffffff",
            AppbarBackground = "#0b2532",
            AppbarText = "#f8fbfb",
            DrawerBackground = "#0d2535",
            DrawerText = "#edf8f7",
            Success = "#2d8659",
            Warning = "#c88515",
            Error = "#b44941",
            Info = "#277da1"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Manrope", "Segoe UI", "sans-serif"]
            },
            H1 = new H1Typography
            {
                FontFamily = ["Manrope", "Segoe UI", "sans-serif"],
                FontWeight = "800"
            },
            H2 = new H2Typography
            {
                FontFamily = ["Manrope", "Segoe UI", "sans-serif"],
                FontWeight = "800"
            },
            H3 = new H3Typography
            {
                FontFamily = ["Manrope", "Segoe UI", "sans-serif"],
                FontWeight = "700"
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Manrope", "Segoe UI", "sans-serif"],
                FontWeight = "700",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "18px"
        }
    };
}
