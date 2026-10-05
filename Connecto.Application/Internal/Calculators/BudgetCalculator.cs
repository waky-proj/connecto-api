using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    /// <summary>
    /// Calculates price alignment only for compatible, explicit listing terms.
    /// </summary>
    public class BudgetCalculator : ICalculator
    {
        private const decimal BudgetWeight = 10m;

        public void Calculate(Need need, Offering offering, MatchScore score)
        {
            // The match service excludes persisted legacy listings. Retaining the historic
            // calculation when both objects are unpersisted keeps the pure calculator usable.
            if (need.PricingBasis != PricingBasis.LegacyUnknown || offering.PricingBasis != PricingBasis.LegacyUnknown)
            {
                if (need.PricingBasis == PricingBasis.LegacyUnknown ||
                    offering.PricingBasis == PricingBasis.LegacyUnknown ||
                    !string.Equals(need.Currency, offering.Currency, StringComparison.OrdinalIgnoreCase) ||
                    need.PricingBasis != offering.PricingBasis)
                {
                    score.UpdateBudget(0);
                    return;
                }

                if (need.PricingBasis is PricingBasis.Free or PricingBasis.Negotiable)
                {
                    score.UpdateBudget(BudgetWeight);
                    return;
                }
            }

            if (need.BudgetMin is null && need.BudgetMax is null)
            {
                score.UpdateBudget(0);
                return;
            }

            if (offering.PriceMin is null && offering.PriceMax is null)
            {
                score.UpdateBudget(0);
                return;
            }

            var needMin = need.BudgetMin ?? 0m;
            var needMax = need.BudgetMax ?? decimal.MaxValue;
            var offerMin = offering.PriceMin ?? 0m;
            var offerMax = offering.PriceMax ?? decimal.MaxValue;
            score.UpdateBudget(offerMin <= needMax && offerMax >= needMin ? BudgetWeight : 0);
        }

        public static decimal Weight => BudgetWeight * 0.7m;

        public bool IsMatch(MatchScore score) => score.Budget >= Weight;

        public string GetReason => "Price is compatible with the stated budget";
    }
}
