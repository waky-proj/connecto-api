using Connecto.Domain.AI;

namespace Connecto.Application.Internal.AI;

/// <summary>
/// The adapter remains unavailable until real extraction and validation are implemented.
/// Configuring a key must never turn a placeholder into a successful suggestion.
/// </summary>
public sealed class OpenAIListingParser : IListingParser
{
    public Task<ListingParseResult> ParseNeedAsync(string title, string description,
        CancellationToken cancellationToken = default) => Unavailable(cancellationToken);

    public Task<ListingParseResult> ParseOfferingAsync(string title, string description,
        CancellationToken cancellationToken = default) => Unavailable(cancellationToken);

    private static Task<ListingParseResult> Unavailable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ListingParsingUnavailableException();
    }
}
