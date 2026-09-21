using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    public class ReputationCalculator : ICalculator
    {
        private const decimal ReputationWeight = 5m;
        public void Calculate(Need need,
            Offering offering, MatchScore score)
        {
            score.UpdateReputation(Math.Clamp(
                offering.ReputationScore / 100m,
                0,
                1) * ReputationWeight);
        }

        private static decimal Weight => ReputationWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Reputation >= Weight;

        public string GetReason => "Acceptable reputation";
    }
}
