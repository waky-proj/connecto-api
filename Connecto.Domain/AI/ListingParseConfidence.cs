namespace Connecto.Domain.AI
{
    /// <summary>
    /// Represents the confidence score for a parsed listing attribute or result.
    /// </summary>
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
