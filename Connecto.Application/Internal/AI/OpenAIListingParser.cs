using Connecto.Domain.AI;
using Connecto.Domain.Entities;
using System.Reflection;

namespace Connecto.Application.Internal.AI
{
    /// <summary>
    ///     You extract structured marketplace information from natural-language listings.
    ///     Your job is to identify facts explicitly stated or strongly implied by the listing.
    ///     
    ///     Rules:
    ///     1. Do not invent skills.
    ///     2. Do not invent prices.
    ///     3. Do not invent dates.
    ///     4. If information is missing, return null.
    ///     5. Normalize equivalent skills where possible.
    ///     6. Use a concise marketplace category.
    ///     7. Separate required skills from general description.
    ///     8. Do not make decisions about whether the listing is a good match.
    ///     9. Return only the requested structured output.
    /// </summary>
    /// <summary>
    /// Parses unstructured listing text by extracting structured marketplace information.
    /// </summary>
    public class OpenAIListingParser : IListingParser
    {
        /// <summary>
        ///     Extract:
        ///     - category
        ///     - required skills
        ///     - requirements
        ///     - budget range
        ///     - location
        ///     - deadline
        /// </summary>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ListingParseResult> ParseNeedAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default)
        {
            return await ParseAsync(
                "need",
                title,
                description,
                cancellationToken);
        }

        /// <summary>
        ///     Extract:
        ///     - category
        ///     - skills
        ///     - capabilities
        ///     - price range
        ///     - location
        ///     - remote availability
        ///     - availability dates
        ///     - experience
        /// </summary>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ListingParseResult> ParseOfferingAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default)
        {
            return await ParseAsync(
                "offering",
                title,
                description,
                cancellationToken);
        }

        private async Task<ListingParseResult> ParseAsync(
        string listingType,
        string title,
        string description,
        CancellationToken cancellationToken)
        {
            var prompt = ListingParserPrompt.Build(
                listingType,
                title,
                description);

            // Call the configured OpenAI model using structured output.
            // Deserialize the response into ListingParseResult.

            var result = new ListingParseResult
            {
                Category = "design"
            }; //await CallModelAsync(prompt, cancellationToken);

            ListingParseValidator.Validate(result);

            return result;
        }
    }
}
