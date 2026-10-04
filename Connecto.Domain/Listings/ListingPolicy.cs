using Connecto.Domain.Entities;

namespace Connecto.Domain.Listings;

public static class ListingPolicy
{
    public const string Currency = "NZD";

    public static readonly IReadOnlySet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
    {
        "Home and Garden", "Household Support", "Technology", "Learning", "Creative"
    };

    public static PricingBasis ParsePricingBasis(string? value)
    {
        if (!Enum.TryParse<PricingBasis>(value, true, out var basis) || basis == PricingBasis.LegacyUnknown)
            throw new ArgumentException("PricingBasis must be fixed, hourly, free, or negotiable.");
        return basis;
    }

    public static void ValidateTerms(string category, string? currency, PricingBasis basis,
        decimal? minimum, decimal? maximum, decimal? estimatedHours, bool requiresEstimatedHours)
    {
        if (!Categories.Contains(category))
            throw new ArgumentException("Category is not supported by the managed taxonomy.");
        if (!string.Equals(currency, Currency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Currency must be NZD.");
        if (minimum < 0 || maximum < 0 || (minimum.HasValue && maximum.HasValue && minimum > maximum))
            throw new ArgumentException("Price range is invalid.");
        if (basis is PricingBasis.Free or PricingBasis.Negotiable)
        {
            if (minimum.HasValue || maximum.HasValue)
                throw new ArgumentException("Free and negotiable listings cannot include a price range.");
        }
        else if (!minimum.HasValue || !maximum.HasValue)
            throw new ArgumentException("Fixed and hourly listings require a price range.");
        if (requiresEstimatedHours && basis == PricingBasis.Hourly && (!estimatedHours.HasValue || estimatedHours <= 0))
            throw new ArgumentException("Hourly listings require estimated hours.");
        if ((basis != PricingBasis.Hourly || !requiresEstimatedHours) && estimatedHours.HasValue)
            throw new ArgumentException("Estimated hours are only accepted for hourly listings.");
    }
}
