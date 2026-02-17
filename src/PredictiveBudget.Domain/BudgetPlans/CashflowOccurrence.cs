using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

public sealed record CashflowOccurrence(
    DateOnly Date,
    string Name,
    TransactionDirection Direction,
    Money Amount,
    OccurrenceSource Source,
    Guid SourceId);
