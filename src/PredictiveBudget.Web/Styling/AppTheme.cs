using MudBlazor;

namespace PredictiveBudget.Web.Styling;

/// <summary>
/// Centralizes the shared MudBlazor theme used by the interactive UI.
/// </summary>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        Typography = new Typography
        {
            Button = new ButtonTypography
            {
                FontWeight = "700",
                TextTransform = "none"
            }
        }
    };
}
