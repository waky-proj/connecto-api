using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    public class AvailabilityCalculator : ICalculator
    {
        private const decimal AvailabilityWeight = 15m;

        public void Calculate(Need need,
            Offering offering, MatchScore score)
        {
            if (need.Deadline is null)
            {
                score.UpdateAvailability(AvailabilityWeight);
                return;
            }

            if (offering.AvailableFrom is null)
            {
                score.UpdateAvailability(AvailabilityWeight * 0.5m);
                return;
            }

            score.UpdateAvailability(offering.AvailableFrom <= need.Deadline
                ? AvailabilityWeight
                : 0);
        }

        public static decimal Weight => AvailabilityWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Availability >= Weight;

        public string GetReason => "Available within the required timeframe";
    }
}
