using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

/// <summary>
/// Represents a concrete cashflow event after recurring rules and overrides have been expanded.
/// </summary>
public sealed record CashflowOccurrence(
    DateOnly Date,
    DateOnly OriginalDate,
    string Name,
    TransactionDirection Direction,
    Money Amount,
    int AlertDaysBefore,
    OccurrenceSource Source,
    Guid SourceId);
