using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Immutable per-call effective LLM configuration. Built by
/// <see cref="AgentExecutionService"/> immediately before each request from
/// <see cref="ISettingsService"/>, <see cref="ISecretStore"/>, and environment
/// variables. Not registered as a DI singleton.
/// </summary>
public sealed class AgentExecutionOptions
{
    /// <summary>
    /// Base URL of the OpenAI-compatible API.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Model identifier to use for chat completions.
    /// </summary>
    public string Model { get; init; } = "gpt-4o-mini";
}
