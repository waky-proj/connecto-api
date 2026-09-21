using Connecto.Domain.Entities;
using Connecto.Domain.Extensions;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    public class SkillCalculator : ICalculator
    {
        private const decimal SkillsWeight = 35m;
        public void Calculate(Need need, Offering offering, MatchScore score)
        {
            if (need.Skills.Count == 0)
            {
                score.UpdateSkill(SkillsWeight);
                return;
            }

            var required = need.Skills
                .Select(x => x.Skill.ToFriendlyString())
                .ToHashSet();

            var offered = offering.Skills
                .Select(x => x.Skill.ToFriendlyString())
                .ToHashSet();

            var matched = required.Intersect(offered).Count();

            var ratio = (decimal)matched / required.Count;

            score.UpdateSkill(ratio * SkillsWeight);
        }

        private static decimal Weight => SkillsWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Skills >= Weight;

        public string GetReason => "Strong skill match";
    }
}
