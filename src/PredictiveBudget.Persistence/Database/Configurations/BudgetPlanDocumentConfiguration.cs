using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PredictiveBudget.Persistence.Documents;

namespace PredictiveBudget.Persistence.Database.Configurations;

/// <summary>
/// Configures storage constraints for serialized budget plan documents.
/// </summary>
internal sealed class BudgetPlanDocumentConfiguration : IEntityTypeConfiguration<BudgetPlanDocument>
{
    public void Configure(EntityTypeBuilder<BudgetPlanDocument> builder)
    {
        builder.HasKey(document => document.PlanId);
        builder.Property(document => document.Name).HasMaxLength(200);
        builder.Property(document => document.Currency).HasMaxLength(12);
        builder.Property(document => document.Json).IsRequired();
    }
}
