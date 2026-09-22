namespace Connecto.Domain.Matching
{
    /// <summary>
    /// Tracks the accumulated score and criteria values used by the matching engine.
    /// </summary>
    public sealed record MatchScore
    {
        public decimal Skills { get; set; }

        public decimal Requirements { get; set; }

        public decimal Availability { get; set; }

        public decimal Budget { get; set; }

        public decimal Location { get; set; }

        public decimal Experience { get; set; }

        public decimal Reputation { get; set; }

        public decimal Total =>
            Skills +
            Requirements +
            Availability +
            Budget +
            Location +
            Experience +
            Reputation;

        public void UpdateSkill(decimal skill) => Skills = skill;

        public void UpdateRequirement(decimal requirement) => Requirements = requirement;

        public void UpdateAvailability(decimal availability) => Availability = availability;

        public void UpdateBudget(decimal budget) => Budget = budget;

        public void UpdateLocation(decimal location) => Location = location;

        public void UpdateExperience(decimal experience) => Experience = experience;

        public void UpdateReputation(decimal reputation) => Reputation = reputation;
    }
}
