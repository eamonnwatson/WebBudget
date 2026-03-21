using MudBlazor;

namespace PredictiveBudget.Web.Styling;

public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#0d6efd",
            Secondary = "#6c757d",
            Tertiary = "#20c997",
            Background = "#212529",
            Surface = "#2b3035",
            AppbarBackground = "#212529",
            AppbarText = "#f8f9fa",
            DrawerBackground = "#1c1f23",
            DrawerText = "#f8f9fa",
            Success = "#198754",
            Warning = "#ffc107",
            Error = "#dc3545",
            Info = "#0dcaf0",
            TextPrimary = "rgba(248, 249, 250, 0.95)",
            TextSecondary = "rgba(248, 249, 250, 0.72)"
        },
        Typography = new Typography
        {
            Button = new ButtonTypography
            {
                FontWeight = "600",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px"
        }
    };
}
