using Connecto.Domain.AI;

namespace Connecto.Application.Internal.AI
{
    public sealed class ListingParseService(IListingParser parser) : IListingParseService
    {
        public Task<ListingParseResult> ParseNeedAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default)
        {
            return parser.ParseNeedAsync(
                title,
                description,
                cancellationToken);
        }

        public Task<ListingParseResult> ParseOfferingAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default)
        {
            return parser.ParseOfferingAsync(
                title,
                description,
                cancellationToken);
        }
    }
}
