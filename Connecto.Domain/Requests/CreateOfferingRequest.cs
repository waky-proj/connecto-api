namespace Connecto.Domain.Requests
{
    public sealed record CreateOfferingRequest(
        string Title,
        string Description,
        string Category,
        IReadOnlyCollection<string> Skills,
        decimal? PriceMin,
        decimal? PriceMax,
        string? Location,
        bool RemoteAvailable,
        DateTime? AvailableFrom,
        DateTime? AvailableUntil,
        int ExperienceYears);
}
