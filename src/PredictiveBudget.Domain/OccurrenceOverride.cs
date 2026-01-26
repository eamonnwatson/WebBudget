using System;
using System.Collections.Generic;
using System.Text;

namespace PredictiveBudget.Domain;

public sealed class OccurrenceOverride
{
    public Guid OverrideId { get; }
    public Guid PlanId { get; }

    public OccurrenceSource Source { get; }
    public Guid SourceId { get; }              // RuleId or TransactionId
    public DateOnly OriginalDate { get; }      // The occurrence date being overridden

    public OverrideAction Action { get; }
    public DateOnly? NewDate { get; }
    public Money? NewAmount { get; }
    public string? NewName { get; }

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
}