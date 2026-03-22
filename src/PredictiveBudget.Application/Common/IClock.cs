namespace PredictiveBudget.Application.Common;

/// <summary>
/// Abstracts the current local date for application workflows and tests.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Returns today's date in the host environment.
    /// </summary>
    DateOnly Today();
}
