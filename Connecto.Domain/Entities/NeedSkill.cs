namespace Connecto.Domain.Entities
{
    public sealed record NeedSkill
    {
        public NeedSkill() { }

        public NeedSkill(Guid needId, Skill skill)
        {
            Id = Guid.NewGuid();
            NeedId = needId;
            Skill = skill;
        }
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid NeedId { get; set; }
        public Skill Skill { get; set; }
        public Need Need { get; set; }
    }
}
