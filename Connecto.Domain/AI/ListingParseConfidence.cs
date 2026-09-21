namespace Connecto.Domain.AI
{
    public sealed record ListingParseConfidence
    {
        public decimal Category { get; init; }

        public decimal Skills { get; init; }

        public decimal Budget { get; init; }

        public decimal Location { get; init; }

        public decimal Date { get; init; }

        public decimal Overall { get; init; }
    }
}
