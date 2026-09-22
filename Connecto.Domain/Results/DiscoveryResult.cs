using Connecto.Domain.AI;
using Connecto.Domain.Matching;

namespace Connecto.Domain.Results
{
    /// <summary>
    /// Represents the discovery result returned after evaluating potential opportunities.
    /// </summary>
    public sealed record DiscoveryResult
    {
        public required ListingParseResult ParsedNeed { get; init; }

        public IReadOnlyCollection<MatchResult> Matches { get; init; } = [];
    }
}
