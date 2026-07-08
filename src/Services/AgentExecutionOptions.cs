namespace Zaide.Services;

/// <summary>
/// Narrow shared configuration for the minimal OpenAI-compatible execution
/// path. Populated from environment variables in <c>Program.cs</c>.
/// </summary>
public sealed class AgentExecutionOptions
{
    /// <summary>
    /// Base URL of the OpenAI-compatible API.
    /// Default: <c>https://api.openai.com/v1</c>
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// API key for authentication. Must be non-empty for successful requests.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier to use for chat completions.
    /// Default: <c>gpt-4o-mini</c>
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";
}