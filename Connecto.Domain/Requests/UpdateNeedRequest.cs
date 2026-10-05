namespace Connecto.Domain.Requests;

public sealed record UpdateNeedRequest(
    int Revision,
    string Title,
    string Description,
    string Category,
    IReadOnlyCollection<string> Skills,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    string? PricingBasis,
    decimal? EstimatedHours,
    string? Location,
    DateTime? Deadline);
