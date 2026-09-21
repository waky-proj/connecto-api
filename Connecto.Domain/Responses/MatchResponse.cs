namespace Connecto.Domain.Responses
{
    public sealed record MatchResponse(
        Guid OfferingId,
        decimal Score,
        MatchBreakdown Breakdown,
        IReadOnlyCollection<string> Reasons);

    public sealed record MatchBreakdown(
        decimal Skills,
        decimal Requirements,
        decimal Availability,
        decimal Budget,
        decimal Location,
        decimal Experience,
        decimal Reputation);
}
