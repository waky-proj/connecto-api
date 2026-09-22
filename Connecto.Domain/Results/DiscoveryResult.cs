using Connecto.Domain.AI;
using Connecto.Domain.Matching;

namespace Connecto.Domain.Results
{
    public sealed record DiscoveryResult
    {
        public required ListingParseResult ParsedNeed { get; init; }

        public IReadOnlyCollection<MatchResult> Matches { get; init; } = [];
    }
}
