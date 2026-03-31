using System.Globalization;
using Microsoft.AspNetCore.Components;
using PredictiveBudget.Domain.Forecasting;

namespace PredictiveBudget.Web.Components.Shared;

public partial class ForecastTrendChart : ComponentBase
{
    private const double ViewWidth = 980d;
    private const double ViewHeight = 410d;
    private const double PlotLeft = 128d;
    private const double PlotRight = 24d;
    private const double PlotTop = 34d;
    private const double PlotBottom = 92d;
    private const double XAxisLabelAngle = -30d;

    private readonly string areaGradientId = $"forecast-area-{Guid.NewGuid():N}";

    private string areaPath = string.Empty;
    private string linePath = string.Empty;
    private string TooltipClass => hoveredPoint is not null && hoveredPoint.YPercent < 28d
        ? "forecast-trend-chart__tooltip forecast-trend-chart__tooltip-bottom"
        : "forecast-trend-chart__tooltip";

    private IReadOnlyList<AxisGuide> guides = [];
    private IReadOnlyList<ChartPlotPoint> plotPoints = [];
    private IReadOnlyList<XAxisLabel> xAxisLabels = [];
    private ChartPlotPoint? hoveredPoint;
    private HighlightMarker? belowZeroMarker;
    private HighlightMarker? maxMarker;
    private HighlightMarker? minMarker;
    private double zeroLineY;
    private double zeroStopOffset;

    [Parameter]
    public string Currency { get; set; } = "CAD";

    [Parameter]
    public DateOnly? FirstBelowZeroDate { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<DailyBalancePoint> Points { get; set; } = [];

    private static double PlotBottomBoundary => ViewHeight - PlotBottom;

    private static double PlotHeight => ViewHeight - PlotTop - PlotBottom;

    private static double PlotRightBoundary => ViewWidth - PlotRight;

    private static double PlotWidth => ViewWidth - PlotLeft - PlotRight;

    private static double AxisLabelX => PlotLeft - 14d;

    private bool HasData => plotPoints.Count > 0;

    protected override void OnParametersSet()
    {
        hoveredPoint = null;

        if (Points.Count == 0)
        {
            areaPath = string.Empty;
            linePath = string.Empty;
            guides = [];
            plotPoints = [];
            xAxisLabels = [];
            minMarker = null;
            maxMarker = null;
            belowZeroMarker = null;
            zeroLineY = PlotBottomBoundary;
            zeroStopOffset = 100d;
            return;
        }

        var minimumBalance = Points.Min(point => point.EndOfDayBalance.Amount);
        var maximumBalance = Points.Max(point => point.EndOfDayBalance.Amount);
        var yAxisScale = BuildYAxisScale(minimumBalance, maximumBalance);
        decimal visibleMinimum = yAxisScale.VisibleMinimum;
        decimal visibleMaximum = yAxisScale.VisibleMaximum;

        double stepX = Points.Count == 1 ? 0d : PlotWidth / (Points.Count - 1d);
        double visibleRange = (double)(visibleMaximum - visibleMinimum);

        plotPoints = Points
            .Select((point, index) =>
            {
                double balance = (double)point.EndOfDayBalance.Amount;
                double x = PlotLeft + (index * stepX);
                double y = PlotTop + (((double)visibleMaximum - balance) / visibleRange * PlotHeight);

                return new ChartPlotPoint(
                    point.Date,
                    point.EndOfDayBalance.Amount,
                    x,
                    y,
                    x / ViewWidth * 100d,
                    y / ViewHeight * 100d,
                    FormatMoney(point.EndOfDayBalance.Amount, Currency),
                    point.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture));
            })
            .ToList();

        zeroLineY = PlotTop + (((double)visibleMaximum - 0d) / visibleRange * PlotHeight);
        zeroStopOffset = Math.Clamp((zeroLineY - PlotTop) / PlotHeight * 100d, 0d, 100d);
        linePath = BuildLinePath(plotPoints);
        areaPath = BuildAreaPath(plotPoints, zeroLineY);
        guides = BuildGuides(yAxisScale, Currency);
        xAxisLabels = BuildXAxisLabels(plotPoints);
        minMarker = CreateMarker("Low", plotPoints.MinBy(point => point.Balance), "low");
        maxMarker = CreateMarker("High", plotPoints.MaxBy(point => point.Balance), "high");

        if (minMarker is not null && maxMarker is not null && minMarker.X == maxMarker.X && minMarker.Y == maxMarker.Y)
        {
            maxMarker = null;
        }

        var belowZeroPoint = FirstBelowZeroDate.HasValue
            ? plotPoints.FirstOrDefault(point => point.Date == FirstBelowZeroDate.Value)
            : null;

        belowZeroMarker = belowZeroPoint is null
            ? null
            : new HighlightMarker("Risk", belowZeroPoint.BalanceLabel, belowZeroPoint.DateLabel, belowZeroPoint.X, belowZeroPoint.Y, "risk");
    }

    private static string BuildAreaPath(IReadOnlyList<ChartPlotPoint> points, double zeroY)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {points[0].X:F2} {zeroY:F2} L {string.Join(" L ", points.Select(point => $"{point.X:F2} {point.Y:F2}"))} L {points[^1].X:F2} {zeroY:F2} Z");
    }

    private static IReadOnlyList<AxisGuide> BuildGuides(YAxisScale scale, string currency)
    {
        int stepCount = Math.Max(1, (int)decimal.Round((scale.VisibleMaximum - scale.VisibleMinimum) / scale.Step, 0, MidpointRounding.AwayFromZero));
        var output = new List<AxisGuide>(stepCount + 1);

        for (int index = 0; index <= stepCount; index++)
        {
            double ratio = stepCount == 0 ? 0d : index / (double)stepCount;
            decimal value = index == stepCount
                ? scale.VisibleMinimum
                : scale.VisibleMaximum - (scale.Step * index);
            double y = PlotTop + (ratio * PlotHeight);

            output.Add(new AxisGuide(y, FormatAxisMoney(value, currency, scale.LabelDecimals), value == 0m));
        }

        return output;
    }

    private static YAxisScale BuildYAxisScale(decimal minimumBalance, decimal maximumBalance)
    {
        decimal floor = Math.Min(minimumBalance, 0m);
        decimal ceiling = Math.Max(maximumBalance, 0m);
        decimal range = ceiling - floor;
        decimal padding = range == 0m ? 1m : Math.Max(range * 0.12m, 1m);
        decimal rawMinimum = floor - padding;
        decimal rawMaximum = ceiling + padding;
        decimal roughStep = (rawMaximum - rawMinimum) / 4m;
        decimal step = NiceStep(roughStep);
        decimal visibleMinimum = decimal.Floor(rawMinimum / step) * step;
        decimal visibleMaximum = decimal.Ceiling(rawMaximum / step) * step;

        return new YAxisScale(visibleMinimum, visibleMaximum, step, GetAxisLabelDecimals(step));
    }

    private static string BuildLinePath(IReadOnlyList<ChartPlotPoint> points)
        => points.Count switch
        {
            0 => string.Empty,
            1 => string.Create(CultureInfo.InvariantCulture, $"M {points[0].X:F2} {points[0].Y:F2} L {points[0].X:F2} {points[0].Y:F2}"),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"M {points[0].X:F2} {points[0].Y:F2} L {string.Join(" L ", points.Skip(1).Select(point => $"{point.X:F2} {point.Y:F2}"))}")
        };

    private static IReadOnlyList<XAxisLabel> BuildXAxisLabels(IReadOnlyList<ChartPlotPoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        int spacing = Math.Max(1, points.Count / 5);
        var selectedIndexes = new SortedSet<int> { 0, points.Count - 1 };

        for (int index = 0; index < points.Count; index++)
        {
            bool isMonthBoundary = index > 0 && points[index].Date.Month != points[index - 1].Date.Month;

            if (index % spacing == 0 || isMonthBoundary)
            {
                selectedIndexes.Add(index);
            }
        }

        return selectedIndexes
            .Select(index => new XAxisLabel(points[index].X, points[index].Date.ToString("MMM d", CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static string FormatMoney(decimal amount, string currency)
        => $"{amount:N2} {currency}";

    private static string FormatAxisMoney(decimal amount, string currency, int decimals)
        => $"{amount.ToString($"N{decimals}", CultureInfo.InvariantCulture)} {currency}";

    private static int GetAxisLabelDecimals(decimal step)
    {
        if (step >= 1m)
        {
            return 0;
        }

        step = decimal.Abs(step);
        int decimals = 0;

        while (step != decimal.Truncate(step) && decimals < 2)
        {
            step *= 10m;
            decimals++;
        }

        return decimals;
    }

    private static decimal NiceStep(decimal roughStep)
    {
        if (roughStep <= 0m)
        {
            return 1m;
        }

        int exponent = (int)Math.Floor(Math.Log10((double)roughStep));
        decimal powerOfTen = Pow10(exponent);
        decimal normalized = roughStep / powerOfTen;
        decimal niceFraction = normalized switch
        {
            <= 1m => 1m,
            <= 2m => 2m,
            <= 2.5m => 2.5m,
            <= 5m => 5m,
            _ => 10m
        };

        return niceFraction * powerOfTen;
    }

    private static decimal Pow10(int exponent)
    {
        decimal value = 1m;

        if (exponent >= 0)
        {
            for (int index = 0; index < exponent; index++)
            {
                value *= 10m;
            }

            return value;
        }

        for (int index = 0; index < -exponent; index++)
        {
            value /= 10m;
        }

        return value;
    }

    private static string BuildRotationTransform(double angle, double x, double y)
        => string.Create(CultureInfo.InvariantCulture, $"rotate({angle:F0} {x:F2} {y:F2})");

    private static HighlightMarker? CreateMarker(string title, ChartPlotPoint? point, string tone)
    {
        if (point is null)
        {
            return null;
        }

        bool alignBelow = point.Y <= PlotTop + 42d;
        double captionY = alignBelow ? point.Y + 30d : point.Y - 22d;
        double valueY = alignBelow ? point.Y + 46d : point.Y - 8d;

        return new HighlightMarker(title, point.BalanceLabel, point.DateLabel, point.X, point.Y, tone, captionY, valueY);
    }

    private static string FormatPercent(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string BuildTooltipStyle(ChartPlotPoint point)
        => $"left:{FormatPercent(point.XPercent)}%; top:{FormatPercent(point.YPercent)}%;";

    private void ClearHoveredPoint()
        => hoveredPoint = null;

    private void SetHoveredPoint(ChartPlotPoint point)
        => hoveredPoint = point;

    private sealed record AxisGuide(double Y, string Label, bool IsZero);

    private sealed record YAxisScale(decimal VisibleMinimum, decimal VisibleMaximum, decimal Step, int LabelDecimals);

    private sealed record ChartPlotPoint(
        DateOnly Date,
        decimal Balance,
        double X,
        double Y,
        double XPercent,
        double YPercent,
        string BalanceLabel,
        string DateLabel)
    {
        public string Tooltip => $"{DateLabel}: {BalanceLabel}";
    }

    private sealed record HighlightMarker(
        string Title,
        string Value,
        string DateLabel,
        double X,
        double Y,
        string Tone,
        double CaptionY = 0d,
        double ValueY = 0d);

    private sealed record XAxisLabel(double X, string Text);
}
