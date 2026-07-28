using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Maps negotiated ACP transport state into the six-fact capability snapshot.
/// </summary>
internal static class AcpCapabilitySnapshotMapper
{
    public static AgentCapabilitySnapshot CreateInitialSnapshot() =>
        AcpCapabilityRows.CreateInitialSnapshot(
            transportReady: false,
            sessionReady: false,
            contextManifestPresent: false,
            usageObserved: false);

    public static AgentCapabilitySnapshot CreateAfterNegotiation(
        AcpNegotiatedCapabilities negotiated,
        bool contextManifestPresent,
        bool usageObserved,
        int version) =>
        AcpCapabilityRows.CreateInitialSnapshot(
            transportReady: negotiated.SupportsSessionPrompt,
            sessionReady: negotiated.SupportsSessionPrompt,
            contextManifestPresent: contextManifestPresent,
            usageObserved: usageObserved,
            version: version);

    public static AgentCapabilitySnapshot WithUsageObserved(
        AgentCapabilitySnapshot current,
        bool usageObserved)
    {
        if (!usageObserved)
        {
            return current;
        }

        if (current.TryGetState(AgentCapabilityId.UsageReporting, out var existing)
            && existing!.CurrentlyUsable == AgentCapabilityFactValue.Supported)
        {
            return current;
        }

        return current.WithRow(
            AcpCapabilityRows.CreateUsageReportingRow(transportReady: true, usageObserved: true),
            current.Version + 1);
    }
}
