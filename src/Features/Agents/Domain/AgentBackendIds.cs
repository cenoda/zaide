namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Canonical backend identity values shared by adapters and coordinator admission.
/// </summary>
internal static class AgentBackendIds
{
    public const string LegacyOpenAiCompatibleValue = "backend:legacy-openai-compatible";

    public static AgentBackendId LegacyOpenAiCompatible { get; } =
        AgentBackendId.FromValue(LegacyOpenAiCompatibleValue);
}
