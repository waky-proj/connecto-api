using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    /// <summary>
    /// Calculates the experience alignment between a need and an offering.
    /// </summary>
    public sealed class ExperienceCalculator : ICalculator
    {
        private const decimal ExperienceWeight = 5m;

        public void Calculate(Need need,
            Offering offering, MatchScore score)
        {
            score.UpdateExperience(offering.ExperienceYears switch
            {
                >= 5 => ExperienceWeight,
                >= 3 => ExperienceWeight * 0.8m,
                >= 1 => ExperienceWeight * 0.6m,
                _ => 0m
            });
        }

        private static decimal Weight => ExperienceWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Experience >= Weight;

        public string GetReason => "Relevant experience";
    }
}
