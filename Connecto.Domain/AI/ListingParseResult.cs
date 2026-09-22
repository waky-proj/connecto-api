namespace Connecto.Domain.AI
{
    /// <summary>
    /// Represents the structured result produced after parsing a marketplace listing.
    /// </summary>
    public sealed record ListingParseResult
    {
        public required string Category { get; init; }

        public IReadOnlyCollection<string> Skills { get; init; } = [];

        public IReadOnlyCollection<string> Requirements { get; init; } = [];

        public decimal? BudgetMin { get; init; }

        public decimal? BudgetMax { get; init; }

        public string? Location { get; init; }

        public DateTime? Deadline { get; init; }

        public decimal? PriceMin { get; init; }

        public decimal? PriceMax { get; init; }

        public bool? RemoteAvailable { get; init; }

        public DateTime? AvailableFrom { get; init; }

        public DateTime? AvailableUntil { get; init; }

        public int? ExperienceYears { get; init; }

        public ListingParseConfidence Confidence { get; init; } = new();
    }
}
