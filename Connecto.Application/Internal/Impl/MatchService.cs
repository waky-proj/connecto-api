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
                throw new KeyNotFoundException(
                    $"Need '{needId}' was not found.");

            var offerings = await offeringDbService.Query()
                .Where(x => x.Status == Domain.Entities.OfferingStatus.Active)
                .ToListAsync(cancellationToken);

            var result = offerings
                .Select(offering =>
                    matchingEngine.Calculate(need, offering));
            return [..result
                .OrderByDescending(x => x.Score.Total)
                .Take(10)];
        }
    }
}
