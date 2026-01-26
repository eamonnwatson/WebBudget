namespace PredictiveBudget.Domain;

public sealed record DailyBalancePoint(DateOnly Date, Money EndOfDayBalance);
