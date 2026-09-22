using Connecto.Application.Internal.AI;
using Connecto.Domain.AI;
using Connecto.Domain.Entities;
using Connecto.Domain.Results;
using Connecto.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Application.Internal.Impl
{
    /// <summary>
    /// Discovers matching opportunities between needs and offerings for a user.
    /// </summary>
    public sealed class DiscoveryService(
            IListingParseService parser,
            IOfferingDbService offeringDbService,
            IMatchingEngine matchingEngine) : IDiscoveryService
    {
        public async Task<DiscoveryResult> DiscoverNeedsAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default)
        {
            var parsed = await parser.ParseNeedAsync(
                title,
                description,
                cancellationToken);

            var need = CreateTemporaryNeed(parsed);

            var offerings = await offeringDbService.Query()
                .Where(x => x.Status == OfferingStatus.Active)
                .ToListAsync(cancellationToken);

            var matches = offerings
                .Select(x => matchingEngine.Calculate(need, x))
                .OrderByDescending(x => x.Score)
                .Take(10)
                .ToList();

            return new DiscoveryResult
            {
                ParsedNeed = parsed,
                Matches = matches
            };
        }

        private static Need CreateTemporaryNeed(
            ListingParseResult parsed)
        {
            var need = new Need
            {
                Id = Guid.Empty,
                UserId = Guid.Empty,
                Title = "Discovery",
                Description = "Temporary discovery request",
                Category = parsed.Category,
                BudgetMin = parsed.BudgetMin,
                BudgetMax = parsed.BudgetMax,
                Location = parsed.Location,
                Deadline = parsed.Deadline
            };

            foreach (var skill in parsed.Skills)
            {
                if (!Enum.TryParse<Skill>(skill, out var skillEnum))
                {
                    skillEnum = Skill.None;
                }
                need.Skills.Add(new NeedSkill
                {
                    Id = Guid.NewGuid(),
                    NeedId = need.Id,
                    Skill = skillEnum
                });
            }

            return need;
        }
    }
}
