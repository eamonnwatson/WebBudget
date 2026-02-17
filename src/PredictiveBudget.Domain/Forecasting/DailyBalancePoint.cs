using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Forecasting;

public sealed record DailyBalancePoint(DateOnly Date, Money EndOfDayBalance);
