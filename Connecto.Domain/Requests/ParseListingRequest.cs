namespace Connecto.Domain.Requests
{
    /// <summary>
    /// Represents the request payload for parsing a listing into structured marketplace data.
    /// </summary>
    public sealed record ParseListingRequest(
        string Title,
        string Description);
}
