namespace Connecto.Domain.Entities
{
    /// <summary>
    /// Represents a service or capability offered by a user.
    /// </summary>
    public sealed record Offering
    {
        public Offering() { }

        public Offering(Guid userId, string title, string description, string category, List<OfferingSkill> skills, decimal priceMin,
        decimal priceMax, string location, int experienceYears, DateTime? availableFrom, bool remote = false)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Title = title;
            Description = description;
            Category = category;
            Skills = skills;
            PriceMin = priceMin;
            PriceMax = priceMax;
            Location = location;
            ExperienceYears = experienceYears;
            AvailableFrom = availableFrom;
            RemoteAvailable = remote;
        }
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public ICollection<OfferingSkill> Skills { get; set; }

        public decimal? PriceMin { get; set; }

        public decimal? PriceMax { get; set; }

        public string? Currency { get; set; }

        public PricingBasis PricingBasis { get; set; } = PricingBasis.LegacyUnknown;

        public decimal? EstimatedHours { get; set; }

        public int Revision { get; set; }

        public string Location { get; set; }

        public int ExperienceYears { get; set; }

        public DateTime? AvailableFrom { get; set; }

        public DateTime? AvailableUntil { get; set; }

        public decimal ReputationScore { get; set; } = 0;

        public bool RemoteAvailable { get; set; }

        public OfferingStatus Status { get; set; } = OfferingStatus.Active;

        public User User { get; set; }
    }

    public enum AvailabilityStatus
    {
        Immediate,
        Unavailable
    }

    public enum OfferingStatus
    {
        Draft,
        Published,
        Paused,
        Closed,
        Active = Published,
        Unavailable = Paused
    }
}
