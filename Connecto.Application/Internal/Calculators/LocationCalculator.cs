using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    /// <summary>
    /// Calculates the geographic fit between a need and an offering.
    /// </summary>
    public class LocationCalculator : ICalculator
    {
        private const decimal LocationWeight = 10m;
        public void Calculate(
            Need need, Offering offering, MatchScore score)
        {
            if (offering.RemoteAvailable)
            {
                score.UpdateLocation(LocationWeight);
                return;
            }

            if (string.IsNullOrWhiteSpace(need.Location) ||
                string.IsNullOrWhiteSpace(offering.Location))
            {
                score.UpdateLocation(0);
                return;
            }

            score.UpdateLocation(string.Equals(
                need.Location,
                offering.Location,
                StringComparison.OrdinalIgnoreCase)
                ? LocationWeight
                : 0);
        }

        private static decimal Weight => LocationWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Location >= Weight;

        public string GetReason => "Location requirements are compatible";
    }
}
