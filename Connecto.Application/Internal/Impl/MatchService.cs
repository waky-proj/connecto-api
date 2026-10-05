using Connecto.Domain.Entities;
using Connecto.Domain.Matching;
using Connecto.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Application.Internal.Impl
{
    /// <summary>
    /// Coordinates match creation, storage, and retrieval between needs and offerings.
    /// </summary>
    public sealed class MatchService(INeedDbService needDbService,
        IOfferingDbService offeringDbService,
        IMatchingEngine matchingEngine) : IMatchService
    {
        public async Task<IReadOnlyCollection<MatchResult>> FindMatchesAsync(
            Guid needId,
            CancellationToken cancellationToken = default)
        {
            var need = await needDbService.GetById(needId);

            if (need is null)
                throw new KeyNotFoundException($"Need '{needId}' was not found.");

            if (need.Status != NeedStatus.Published || need.PricingBasis == PricingBasis.LegacyUnknown ||
                need.Currency != "NZD")
                return [];

            var offerings = await offeringDbService.Query()
                .Where(x => x.UserId != need.UserId && x.Status == OfferingStatus.Published &&
                    x.PricingBasis == need.PricingBasis && x.Currency == need.Currency &&
                    x.Category == need.Category)
                .ToListAsync(cancellationToken);

            return [..offerings
                .Where(offering => IsEligible(need, offering))
                .Select(offering => matchingEngine.Calculate(need, offering))
                .OrderByDescending(x => x.Score.Total)
                .Take(10)];
        }

        public static bool IsEligible(Need need, Offering offering)
        {
            if (need.UserId == offering.UserId || need.Status != NeedStatus.Published ||
                offering.Status != OfferingStatus.Published ||
                need.PricingBasis != offering.PricingBasis ||
                need.PricingBasis == PricingBasis.LegacyUnknown ||
                !string.Equals(need.Currency, offering.Currency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(need.Category, offering.Category, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!offering.RemoteAvailable && !string.IsNullOrWhiteSpace(need.Location) &&
                !string.IsNullOrWhiteSpace(offering.Location) &&
                !string.Equals(need.Location, offering.Location, StringComparison.OrdinalIgnoreCase))
                return false;
            if (need.Deadline is not null && offering.AvailableFrom is not null &&
                offering.AvailableFrom > need.Deadline) return false;
            if (offering.AvailableUntil is not null && offering.AvailableUntil < DateTime.UtcNow)
                return false;
            if (need.PricingBasis == PricingBasis.Hourly && need.EstimatedHours is null or <= 0)
                return false;
            if (need.PricingBasis is PricingBasis.Fixed or PricingBasis.Hourly &&
                need.BudgetMax is not null && offering.PriceMin is not null &&
                offering.PriceMin > need.BudgetMax) return false;
            if (need.PricingBasis is PricingBasis.Fixed or PricingBasis.Hourly &&
                need.BudgetMin is not null && offering.PriceMax is not null &&
                offering.PriceMax < need.BudgetMin) return false;
            return true;
        }
    }
}
