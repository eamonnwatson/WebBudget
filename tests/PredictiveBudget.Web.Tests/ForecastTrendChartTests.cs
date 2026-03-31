using PredictiveBudget.Web.Components.Shared;
using PredictiveBudget.Web.Tests.TestSupport;

namespace PredictiveBudget.Web.Tests;

/// <summary>
/// Verifies the forecast chart's axis scale stays readable and includes zero.
/// </summary>
public sealed class ForecastTrendChartTests
{
    [Fact]
    public void BuildGuides_RoundsLabelsAndIncludesZero()
    {
        var scale = ReflectionTestHelper.InvokeStatic<object>(
            typeof(ForecastTrendChart),
            "BuildYAxisScale",
            -132.06m,
            2408.21m);

        var guides = ReflectionTestHelper.InvokeStatic<IReadOnlyList<object>>(
            typeof(ForecastTrendChart),
            "BuildGuides",
            scale,
            "CAD");

        Assert.Equal(["3,000 CAD", "2,000 CAD", "1,000 CAD", "0 CAD", "-1,000 CAD"], guides
            .Select(guide => ReflectionTestHelper.GetPropertyValue<string>(guide, "Label"))
            .ToArray());
        Assert.Single(guides, guide => ReflectionTestHelper.GetPropertyValue<bool>(guide, "IsZero"));
    }
}
