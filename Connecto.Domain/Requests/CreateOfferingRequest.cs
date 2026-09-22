namespace Connecto.Domain.Requests
{
    /// <summary>
    /// Represents the request payload for creating a new offering.
    /// </summary>
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
