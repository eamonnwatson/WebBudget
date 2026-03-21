using PredictiveBudget.Application.Common;

namespace PredictiveBudget.Web.Services;

public sealed class SystemClock : IClock
{
    public DateOnly Today()
        => DateOnly.FromDateTime(DateTime.Today);
}
