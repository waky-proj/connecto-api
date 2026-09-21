namespace Connecto.Application.Internal.AI
{
    public static class ListingParserPrompt
    {
        public static string Build(
            string listingType,
            string title,
            string description)
        {
            return $"""
        You are a structured data extraction service for a marketplace.

        Listing type: {listingType}

        Extract information from the user's listing.

        Rules:
        - Extract only information supported by the input.
        - Never invent a skill, price, date, location, or experience level.
        - If information is unavailable, return null.
        - Normalize skill names.
        - Use a concise marketplace category.
        - Do not decide whether the listing matches another listing.
        - Do not recommend providers.
        - Return only the requested structured fields.

        TITLE:
        {title}

        DESCRIPTION:
        {description}
        """;
        }
    }
}
