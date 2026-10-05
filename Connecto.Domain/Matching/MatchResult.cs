namespace Connecto.Domain.Matching
{
    /// <summary>
    /// Represents the final weighted outcome for a need and offering comparison.
    /// </summary>
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

        public IReadOnlyCollection<string> Unknowns { get; set; } = [];

        public string PolicyVersion { get; set; } = "s3-v1";
    }
}
