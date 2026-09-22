namespace Connecto.Domain.Entities
{
    /// <summary>
    /// Represents a skill attached to an offering.
    /// </summary>
    public sealed record OfferingSkill
    {
        public OfferingSkill() { }

        public OfferingSkill(Guid offeringId, Skill skill)
        {
            Id = Guid.NewGuid();
            OfferingId = offeringId;
            Skill = skill;
        }

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OfferingId { get; set; }
        public Skill Skill { get; set; }

        public Offering Offering { get; set; }
    }
}
