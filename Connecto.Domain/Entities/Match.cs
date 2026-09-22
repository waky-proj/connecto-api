namespace Connecto.Domain.Entities
{
    /// <summary>
    /// Represents a recorded match between a user need and a corresponding offering.
    /// </summary>
    public sealed class Match
    {
        public Match() { }

        public Guid Id { get; init; }
        public Guid NeedId { get; init; }
        public Guid OfferingId { get; init; }
        public decimal Score { get; init; }
        public decimal SkillsScore { get; init; }
        public decimal RequirementsScore { get; init; }
        public decimal AvailabilityScore { get; init; }
        public decimal BudgetScore { get; init; }
        public decimal LocationScore { get; init; }
        public decimal ExperienceScore { get; init; }
        public decimal ReputationScore { get; init; }
        public DateTime CalculatedAt { get; init; }
        public MatchStatus Status { get; init; }
    }

    public enum MatchStatus
    {
        Pending,
        Accepted,
        Rejected
    }
}
