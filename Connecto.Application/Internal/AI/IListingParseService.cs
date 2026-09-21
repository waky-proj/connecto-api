using Connecto.Domain.AI;

namespace Connecto.Application.Internal.AI
{
    public interface IListingParseService
    {
        Task<ListingParseResult> ParseNeedAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default);

        Task<ListingParseResult> ParseOfferingAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default);
    }
}
