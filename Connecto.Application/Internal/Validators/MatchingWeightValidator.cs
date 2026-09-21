using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Validators
{
    public static class MatchingWeightsValidator
    {
        public static void Validate(MatchingWeights weights)
        {
            if (weights.Total != 100m)
            {
                throw new InvalidOperationException(
                    $"Matching weights must total 100. Actual: {weights.Total}");
            }
        }
    }
}
