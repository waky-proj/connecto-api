namespace Connecto.Domain.AI
{
    /// <summary>
    /// Represents a matched opportunity between a need and an offering.
    /// </summary>
    public sealed record OpportunityMatch
    {
        public Guid SourceId { get; init; }

        public Guid TargetId { get; init; }

        public MatchDirection Direction { get; init; }

        public decimal Score { get; init; }

        public IReadOnlyCollection<string> Reasons { get; init; } = [];
    }

    public enum MatchDirection
    {
        NeedToOffering,
        OfferingToNeed
    }
}
