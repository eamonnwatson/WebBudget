namespace PredictiveBudget.Persistence.Documents;

public sealed class BudgetPlanDocument
{
    public Guid PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; set; }
    public string Json { get; set; } = string.Empty;
}
