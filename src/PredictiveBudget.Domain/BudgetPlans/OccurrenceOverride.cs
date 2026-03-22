using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.BudgetPlans;

/// <summary>
/// Adjusts a single generated or planned occurrence without changing the source rule itself.
/// </summary>
public sealed class OccurrenceOverride
{
    public Guid OverrideId { get; }
    public Guid PlanId { get; }

    public OccurrenceSource Source { get; private set; }
    public Guid SourceId { get; private set; }
    public DateOnly OriginalDate { get; private set; }

    public OverrideAction Action { get; private set; }
    public DateOnly? NewDate { get; private set; }
    public Money? NewAmount { get; private set; }
    public string? NewName { get; private set; }

    public OccurrenceOverride(
        Guid overrideId,
        Guid planId,
        OccurrenceSource source,
        Guid sourceId,
        DateOnly originalDate,
        OverrideAction action,
        DateOnly? newDate = null,
        Money? newAmount = null,
        string? newName = null)
    {
        OverrideId = overrideId;
        PlanId = planId;
        Source = source;
        SourceId = sourceId;
        OriginalDate = originalDate;
        Action = action;
        NewDate = newDate;
        NewAmount = newAmount;
        NewName = newName;
    }

    public void Update(
        OccurrenceSource source,
        Guid sourceId,
        DateOnly originalDate,
        OverrideAction action,
        DateOnly? newDate,
        Money? newAmount,
        string? newName)
    {
        Source = source;
        SourceId = sourceId;
        OriginalDate = originalDate;
        Action = action;
        NewDate = newDate;
        NewAmount = newAmount;
        NewName = newName;
    }
}
