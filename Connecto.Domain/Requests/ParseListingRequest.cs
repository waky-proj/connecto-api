namespace Connecto.Domain.Requests
{
    public sealed record ParseListingRequest(
        string Title,
        string Description);
}
