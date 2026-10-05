namespace Connecto.Domain.Requests;

public sealed record UpdateOfferingRequest(
    int Revision,
    string Title,
    string Description,
    string Category,
    IReadOnlyCollection<string> Skills,
    decimal? PriceMin,
    decimal? PriceMax,
    string? Currency,
    string? PricingBasis,
    decimal? EstimatedHours,
    string? Location,
    bool RemoteAvailable,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    int ExperienceYears);
