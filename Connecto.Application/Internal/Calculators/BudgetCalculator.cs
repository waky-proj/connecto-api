using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    /// <summary>
    /// Calculates the budget alignment between a need and an offering.
    /// </summary>
    public class BudgetCalculator : ICalculator
    {
        private const decimal BudgetWeight = 10m;

        public void Calculate(
            Need need, Offering offering, MatchScore score)
        {
            if (need.BudgetMin is null &&
                need.BudgetMax is null)
            {
                score.UpdateBudget(BudgetWeight);
                return;
            }

            if (offering.PriceMin is null &&
                offering.PriceMax is null)
            {
                score.UpdateBudget(BudgetWeight * 0.5m);
                return;
            }

            var needMin = need.BudgetMin ?? 0m;
            var needMax = need.BudgetMax ?? decimal.MaxValue;

            var offerMin = offering.PriceMin ?? 0m;
            var offerMax = offering.PriceMax ?? decimal.MaxValue;

            var overlaps =
                offerMin <= needMax &&
                offerMax >= needMin;

            score.UpdateBudget(overlaps
                ? BudgetWeight
                : 0);
        }

        public static decimal Weight => BudgetWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Budget >= Weight;

        public string GetReason => "Price is compatible with the stated budget";
    }
}
