namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Canonical backend identity values shared by adapters and coordinator admission.
/// </summary>
internal static class AgentBackendIds
{
    public const string LegacyOpenAiCompatibleValue = "backend:legacy-openai-compatible";

    public const string NativeHarnessValue = "backend:zaide-native-harness";

    public const string AcpValue = "backend:acp";

    public static AgentBackendId LegacyOpenAiCompatible { get; } =
        AgentBackendId.FromValue(LegacyOpenAiCompatibleValue);

    public static AgentBackendId NativeHarness { get; } =
        AgentBackendId.FromValue(NativeHarnessValue);

    public static AgentBackendId Acp { get; } =
        AgentBackendId.FromValue(AcpValue);
}
