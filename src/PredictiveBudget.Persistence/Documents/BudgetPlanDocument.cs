namespace PredictiveBudget.Persistence.Documents;

/// <summary>
/// Stores the serialized state of a budget plan plus lightweight metadata for listing.
/// </summary>
public sealed class BudgetPlanDocument
{
    public Guid PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; set; }
    public string Json { get; set; } = string.Empty;
}
