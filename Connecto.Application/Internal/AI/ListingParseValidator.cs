using Connecto.Domain.AI;

namespace Connecto.Application.Internal.AI
{
    public static class ListingParseValidator
    {
        public static void Validate(ListingParseResult result)
        {
            if (result.Confidence.Overall < 0 || result.Confidence.Overall > 1)
            {
                throw new InvalidOperationException(
                    "AI confidence must be between 0 and 1.");
            }

            if (result.BudgetMin.HasValue &&
                result.BudgetMax.HasValue &&
                result.BudgetMin > result.BudgetMax)
            {
                throw new InvalidOperationException(
                    "AI returned an invalid budget range.");
            }

            if (result.PriceMin.HasValue &&
                result.PriceMax.HasValue &&
                result.PriceMin > result.PriceMax)
            {
                throw new InvalidOperationException(
                    "AI returned an invalid price range.");
            }
        }
    }
}
