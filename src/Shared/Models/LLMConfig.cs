namespace Shared.Models;

/// <summary>
/// Configuration for language model usage.
/// </summary>
public record class LLMConfig
{
    /// <summary>
    /// Determines if OpenAI should be used for agent requests.
    /// </summary>
    public bool UseOpenAI { get; set; }

    /// <summary>
    /// API key used when communicating with OpenAI services.
    /// </summary>
    public string? ApiKey { get; set; }

    public LLMConfig()
    {
    }

    public LLMConfig(bool useOpenAI, string? apiKey)
    {
        UseOpenAI = useOpenAI;
        ApiKey = apiKey;
    }
}
