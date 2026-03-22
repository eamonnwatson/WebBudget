using PredictiveBudget.Application.Common;

namespace PredictiveBudget.Web.Services;

/// <summary>
/// Supplies the current local date for production runtime usage.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateOnly Today()
        => DateOnly.FromDateTime(DateTime.Today);
}
