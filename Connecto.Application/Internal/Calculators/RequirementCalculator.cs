using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    /// <summary>
    /// Evaluates whether the requirement criteria align between a need and an offering.
    /// </summary>
    public class RequirementCalculator : ICalculator
    {
        private const decimal RequirementsWeight = 20m;

        public void Calculate(
            Need need,
            Offering offering, MatchScore score)
        {
            var categoryMatch =
                string.Equals(
                    need.Category,
                    offering.Category,
                    StringComparison.OrdinalIgnoreCase);

            if (categoryMatch)
            {
                score.UpdateRequirement(RequirementsWeight);
            }
        }

        private static decimal Weight => RequirementsWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Requirements >= Weight;

        public string GetReason => "Strong requirement match";
    }
}
