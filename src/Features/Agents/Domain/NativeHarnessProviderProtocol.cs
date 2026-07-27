namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Locked provider and tool-calling protocol constants for the Native Harness.
/// M2 architecture lock: OpenAI-compatible HTTP without a new NuGet dependency.
/// </summary>
internal static class NativeHarnessProviderProtocol
{
    public const string ChatCompletionsPath = "/chat/completions";

    public const string FunctionCallingFormat = "openai-tools";

    public const string StreamingTransport = "sse";

    public const int DefaultMaxTurns = 25;

    public const int DefaultProviderTimeoutSeconds = 120;
}
