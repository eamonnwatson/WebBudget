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
        var buttonTypography = theme.Typography!.Button!;

        Assert.Equal("#0d6efd", theme.PaletteDark!.Primary);
        Assert.Equal("#6c757d", theme.PaletteDark.Secondary);
        Assert.Equal("6px", theme.LayoutProperties!.DefaultBorderRadius);
        Assert.Equal("600", buttonTypography.FontWeight);
        Assert.Equal("none", buttonTypography.TextTransform);
    }
}
