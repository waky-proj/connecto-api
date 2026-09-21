namespace Connecto.Domain.Matching
{
    public sealed class MatchResult(
        Guid NeedId,
        Guid OfferingId,
        MatchScore Score,
        IReadOnlyCollection<string> Reasons)
    {
        public Guid NeedId { get; set; } = NeedId;

        public Guid OfferingId { get; set; } = OfferingId;

        public MatchScore Score { get; set; } = Score;

        public IReadOnlyCollection<string> Reasons { get; set; } = Reasons;
    }
}
