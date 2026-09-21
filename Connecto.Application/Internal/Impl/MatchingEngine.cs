using Connecto.Application.Internal.Calculators;
using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Impl
{
    public sealed class MatchingEngine(IEnumerable<ICalculator> calculators) : IMatchingEngine
    {
        //private readonly MatchingWeights _weights;
        //public MatchingEngine(MatchingWeights weights)
        //{
        //    MatchingWeightsValidator.Validate(weights);
        //    _weights = weights;
        //}

        
        public MatchResult Calculate(Need need, Offering offering)
        {
            var score = new MatchScore();
            var reasons = new List<string>();

            foreach(var calculator in calculators)
            {
                calculator.Calculate(need, offering, score);
                if (!calculator.IsMatch(score)) continue;

                reasons.Add(calculator.GetReason);
            }

            return new MatchResult(
                need.Id,
                offering.Id,
                score,
                reasons);
        }
    }
}
