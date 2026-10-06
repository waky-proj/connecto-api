using Connecto.Domain.Entities;

namespace Connecto.Api.Controllers;

public sealed record DiscoveryOfferDto(
    Guid Id, Guid MemberId, string Title, string Description, string Category, string[] Skills,
    string? ProviderName, string? Location, bool RemoteAvailable,
    decimal? PriceMin, decimal? PriceMax, string? Currency, PricingBasis PricingBasis,
    decimal? EstimatedHours, DateTime? AvailableFrom, DateTime? AvailableUntil,
    int ExperienceYears, int Revision);

public static class DiscoveryOfferProjection
{
    public static DiscoveryOfferDto From(Offering offering)
    {
        var first = offering.User?.FirstName?.Trim();
        var last = offering.User?.LastName?.Trim();
        var name = string.Join(" ", new[] { first, string.IsNullOrWhiteSpace(last) ? null : $"{last[0]}." }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        return new(offering.Id, offering.UserId, offering.Title, offering.Description, offering.Category,
            offering.Skills.Select(x => x.Skill.ToString()).ToArray(),
            name.Length > 0 ? name : null, offering.Location, offering.RemoteAvailable,
            offering.PriceMin, offering.PriceMax, offering.Currency, offering.PricingBasis,
            offering.EstimatedHours, offering.AvailableFrom, offering.AvailableUntil,
            offering.ExperienceYears, offering.Revision);
    }
}
