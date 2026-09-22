namespace Connecto.Domain.AI
{
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
