namespace Connecto.Domain.AI
{
    /// <summary>
    /// Contains configuration values used to connect to the OpenAI service.
    /// </summary>
    public sealed class OpenAIOptions
    {
        public string ApiKey { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;
    }
}
