namespace Connecto.Application.Internal.AI;

public sealed class ListingParsingUnavailableException : Exception
{
    public ListingParsingUnavailableException() : base("Listing parsing is not available.") { }
}
