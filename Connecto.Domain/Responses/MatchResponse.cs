namespace Connecto.Domain.Responses
{
    /// <summary>
    /// Represents the response payload returned for a computed match.
    /// </summary>
    public sealed record MatchResponse(
        Guid OfferingId,
        decimal Score,
        MatchBreakdown Breakdown,
        IReadOnlyCollection<string> Reasons);

    /// <summary>
    /// Represents the detailed breakdown of the matching score components.
    /// </summary>
    public sealed record MatchBreakdown(
        decimal Skills,
        decimal Requirements,
        decimal Availability,
        decimal Budget,
        decimal Location,
        decimal Experience,
        decimal Reputation);
}
