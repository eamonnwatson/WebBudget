using MudBlazor;

namespace PredictiveBudget.Web.Styling;

/// <summary>
/// Centralizes the shared MudBlazor theme used by the interactive UI.
/// </summary>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#356dff",
            Secondary = "#1d9a9f",
            Tertiary = "#f59e0b",
            Background = "#f4f7fb",
            Surface = "#ffffff",
            AppbarBackground = "rgba(255, 255, 255, 0.92)",
            AppbarText = "#10233f",
            DrawerBackground = "#eef3fb",
            DrawerText = "#10233f",
            Success = "#0f9f78",
            Warning = "#d97706",
            Error = "#dc4c64",
            Info = "#2563eb",
            TextPrimary = "rgba(16, 35, 63, 0.96)",
            TextSecondary = "rgba(74, 93, 124, 0.8)"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7aa2ff",
            Secondary = "#63d4c6",
            Tertiary = "#fbbf24",
            Background = "#08111d",
            Surface = "#111c2d",
            AppbarBackground = "rgba(8, 17, 29, 0.84)",
            AppbarText = "#eff6ff",
            DrawerBackground = "#0c1726",
            DrawerText = "#eff6ff",
            Success = "#34d399",
            Warning = "#f59e0b",
            Error = "#fb7185",
            Info = "#67c7ff",
            TextPrimary = "rgba(239, 246, 255, 0.96)",
            TextSecondary = "rgba(191, 204, 223, 0.78)"
        },
        Typography = new Typography
        {
            Button = new ButtonTypography
            {
                FontWeight = "700",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "22px"
        }
    };
}
