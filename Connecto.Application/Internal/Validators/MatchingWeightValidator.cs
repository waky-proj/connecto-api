using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Validators
{
    public static class MatchingWeightsValidator
    {
        public static void Validate(MatchingWeights weights)
        {
            if (string.IsNullOrWhiteSpace(weights.Version) || weights.Total != 100m || new[] { weights.Skills, weights.Requirements,
                weights.Availability, weights.Budget, weights.Location, weights.Experience,
                weights.Reputation }.Any(value => value < 0m))
            {
                throw new InvalidOperationException(
                    $"Matching weights must total 100. Actual: {weights.Total}");
            }
        }
    }
}
