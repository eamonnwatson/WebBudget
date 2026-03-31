using PredictiveBudget.Web.Services;
using PredictiveBudget.Web.Styling;

namespace PredictiveBudget.Web.Tests;

/// <summary>
/// Covers small UI infrastructure pieces such as the system clock and MudBlazor theme.
/// </summary>
public sealed class SystemClockAndThemeTests
{
    [Fact]
    public void SystemClock_Today_ReturnsCurrentDate()
    {
        var clock = new SystemClock();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), clock.Today());
    }

    [Fact]
    public void AppTheme_Theme_ExposesConfiguredLightAndDarkPalettes()
    {
        var theme = AppTheme.Theme;
        var buttonTypography = theme.Typography!.Button!;

        Assert.Equal("#356dff", theme.PaletteLight!.Primary);
        Assert.Equal("#1d9a9f", theme.PaletteLight.Secondary);
        Assert.Equal("#7aa2ff", theme.PaletteDark!.Primary);
        Assert.Equal("#63d4c6", theme.PaletteDark.Secondary);
        Assert.Equal("22px", theme.LayoutProperties!.DefaultBorderRadius);
        Assert.Equal("700", buttonTypography.FontWeight);
        Assert.Equal("none", buttonTypography.TextTransform);
    }
}
