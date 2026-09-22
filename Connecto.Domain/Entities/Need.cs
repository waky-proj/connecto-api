namespace Connecto.Domain.Entities
{
    /// <summary>
    /// Represents a request or requirement raised by a user in the marketplace.
    /// </summary>
    public sealed record Need
    {
        public Need() { }

        public Need(Guid userId, string title, string description, string category, List<NeedSkill> skills, decimal budgetMin,
        decimal budgetMax, string location, DateTime? deadline, bool remoteAllowed)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Title = title;
            Description = description;
            Category = category;
            Skills = skills;
            BudgetMin = budgetMin;
            BudgetMax = budgetMax;
            Location = location;
            Deadline = deadline;
            RemoteAllowed = remoteAllowed;
        }

        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public ICollection<NeedSkill> Skills { get; set; }

        public decimal? BudgetMin { get; set; }

        public decimal? BudgetMax { get; set; }

        public string Location { get; set; }

        public bool RemoteAllowed { get; set; }

        public DateTime? Deadline { get; set; }

        public NeedStatus Status { get; set; } = NeedStatus.Open;

        public User User { get; set; }

    }

    public enum NeedStatus
    {
        Open,
        Matched,
        Closed
    }
}
