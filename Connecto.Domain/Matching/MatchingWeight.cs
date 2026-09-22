namespace Connecto.Domain.Matching
{
    public sealed record MatchingWeights
    {
        public decimal Skills { get; init; } = 35m;
        public decimal Requirements { get; init; } = 20m;
        public decimal Availability { get; init; } = 15m;
        public decimal Budget { get; init; } = 10m;
        public decimal Location { get; init; } = 10m;
        public decimal Experience { get; init; } = 5m;
        public decimal Reputation { get; init; } = 5m;

        public decimal Total =>
            Skills +
            Requirements +
            Availability +
            Budget +
            Location +
            Experience +
            Reputation;
    }
}
