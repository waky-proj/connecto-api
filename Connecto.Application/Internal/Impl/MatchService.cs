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

            if (need.Status != NeedStatus.Published || need.PricingBasis == PricingBasis.LegacyUnknown)
                return [];

            var offerings = await offeringDbService.Query()
                .Where(x => x.Status == OfferingStatus.Published && x.PricingBasis != PricingBasis.LegacyUnknown)
                .ToListAsync(cancellationToken);

            return [..offerings
                .Select(offering => matchingEngine.Calculate(need, offering))
                .OrderByDescending(x => x.Score.Total)
                .Take(10)];
        }
    }
}
