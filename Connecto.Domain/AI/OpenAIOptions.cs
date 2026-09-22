namespace Connecto.Domain.AI
{
    public sealed class OpenAIOptions
    {
        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;
    }
}
