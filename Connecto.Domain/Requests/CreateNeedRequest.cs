namespace Connecto.Domain.Requests
{
    /// <summary>
    /// Represents the request payload for creating a new user need.
    /// </summary>
    public sealed record CreateNeedRequest(
        string Title,
        string Description,
        string Category,
        IReadOnlyCollection<string> Skills,
        decimal? BudgetMin,
        decimal? BudgetMax,
        string? Location,
        DateTime? Deadline,
        string? Currency = null,
        string? PricingBasis = null,
        decimal? EstimatedHours = null);
}
