using PredictiveBudget.Web.Services;
using PredictiveBudget.Web.Styling;

namespace PredictiveBudget.Web.Tests;

public sealed class SystemClockAndThemeTests
{
    [Fact]
    public void SystemClock_Today_ReturnsCurrentDate()
    {
        var clock = new SystemClock();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), clock.Today());
    }

    [Fact]
    public void AppTheme_Theme_ExposesConfiguredPaletteAndTypography()
    {
        var theme = AppTheme.Theme;
        var fontFamily = theme.Typography!.Default!.FontFamily;

        Assert.Equal("#0f766e", theme.PaletteLight!.Primary);
        Assert.Equal("#e0a82e", theme.PaletteLight.Secondary);
        Assert.Equal("18px", theme.LayoutProperties!.DefaultBorderRadius);
        Assert.NotNull(fontFamily);
        Assert.Contains("Manrope", fontFamily);
    }
}
