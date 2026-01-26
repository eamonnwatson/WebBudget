namespace PredictiveBudget.Domain;

public sealed record CashflowOccurrence(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    Money Amount,
    OccurrenceSource Source,
    Guid SourceId);
