using Connecto.Application.Internal.Calculators;
using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Impl
{
    /// <summary>
    /// Applies the configured calculators to score a need and offering pair.
    /// </summary>
    public sealed class MatchingEngine(IEnumerable<ICalculator> calculators, MatchingWeights? weights = null) : IMatchingEngine
    {
        private readonly MatchingWeights _weights = weights ?? new MatchingWeights();

        public MatchResult Calculate(Need need, Offering offering)
        {
            var score = new MatchScore();
            var reasons = new List<string>();

            foreach(var calculator in calculators)
            {
                calculator.Calculate(need, offering, score);
                if (!calculator.IsMatch(score) || !IsEnabled(calculator)) continue;

                reasons.Add(calculator.GetReason);
            }

            score.Skills *= _weights.Skills / 35m;
            score.Requirements *= _weights.Requirements / 20m;
            score.Availability *= _weights.Availability / 15m;
            score.Budget *= _weights.Budget / 10m;
            score.Location *= _weights.Location / 10m;
            score.Experience *= _weights.Experience / 5m;
            score.Reputation *= _weights.Reputation / 5m;

            var unknowns = new List<string>();
            if (offering.AvailableFrom is null) unknowns.Add("Provider availability is unknown");
            if (string.IsNullOrWhiteSpace(offering.Location) && !offering.RemoteAvailable)
                unknowns.Add("Provider location is unknown");
            if (offering.ExperienceYears <= 0) unknowns.Add("Provider experience is not stated");
            if (offering.ReputationScore <= 0) unknowns.Add("No verified reputation data is available");
            if (need.PricingBasis is PricingBasis.Fixed or PricingBasis.Hourly &&
                offering.PriceMin is null && offering.PriceMax is null)
                unknowns.Add("Provider price is unknown");

            return new MatchResult(
                need.Id,
                offering.Id,
                score,
                reasons) { PolicyVersion = _weights.Version, Unknowns = unknowns };
        }

        private bool IsEnabled(ICalculator calculator) => calculator switch
        {
            SkillCalculator => _weights.Skills > 0,
            RequirementCalculator => _weights.Requirements > 0,
            AvailabilityCalculator => _weights.Availability > 0,
            BudgetCalculator => _weights.Budget > 0,
            LocationCalculator => _weights.Location > 0,
            ExperienceCalculator => _weights.Experience > 0,
            ReputationCalculator => _weights.Reputation > 0,
            _ => true
        };
    }
}
