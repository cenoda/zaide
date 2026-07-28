using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Canonical six-fact capability rows for the ACP backend identity.
/// </summary>
internal static class AcpCapabilityRows
{
    public static AgentCapabilitySnapshot CreateInitialSnapshot(
        bool transportReady,
        bool sessionReady,
        bool contextManifestPresent,
        bool usageObserved,
        int version = 1) =>
        AgentCapabilitySnapshot.CreateInitial(
            AgentBackendIds.Acp,
            new[]
            {
                CreateMessageCompletionRow(transportReady, sessionReady),
                CreateCancellationRow(transportReady, sessionReady),
                CreateToolsRow(transportReady, sessionReady),
                CreatePermissionsRow(transportReady),
                CreateIdeContextRow(transportReady, contextManifestPresent),
                CreateStreamingRow(transportReady, sessionReady),
                CreateResumeReconnectRow(),
                CreateUsageReportingRow(transportReady, usageObserved),
                CreateRawTraceRow(),
            },
            version: version);

    public static AgentCapabilitySnapshot CreateUnavailableSnapshot(int version = 1) =>
        AgentCapabilitySnapshot.CreateInitial(
            AgentBackendIds.Acp,
            new[]
            {
                CreateUnavailableRow(AgentCapabilityId.MessageCompletion),
                CreateUnavailableRow(AgentCapabilityId.Cancellation),
                CreateUnavailableRow(AgentCapabilityId.Tools),
                CreateUnavailableRow(AgentCapabilityId.Permissions),
                CreateUnavailableRow(AgentCapabilityId.IdeContext),
                CreateUnavailableRow(AgentCapabilityId.Streaming),
                CreateResumeReconnectRow(),
                CreateUnavailableRow(AgentCapabilityId.UsageReporting),
                CreateRawTraceRow(),
            },
            version: version);

    public static AgentCapabilityRow CreateMessageCompletionRow(bool transportReady, bool sessionReady) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            transportReady && sessionReady
                ? SupportedUsableRow()
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateCancellationRow(bool transportReady, bool sessionReady) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Cancellation,
            transportReady && sessionReady
                ? SupportedUsableRow()
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateToolsRow(bool transportReady, bool sessionReady) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Tools,
            transportReady && sessionReady
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Unknown)
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreatePermissionsRow(bool transportReady) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Permissions,
            transportReady
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.NotSupported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.NotSupported)
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateIdeContextRow(
        bool transportReady,
        bool contextManifestPresent) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.IdeContext,
            ResolveIdeContextState(transportReady, contextManifestPresent));

    public static AgentCapabilityRow CreateStreamingRow(bool transportReady, bool sessionReady) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            transportReady && sessionReady
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Supported)
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateResumeReconnectRow() =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Resume,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.NotSupported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.NotSupported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));

    public static AgentCapabilityRow CreateUsageReportingRow(bool transportReady, bool usageObserved) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.UsageReporting,
            transportReady
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: usageObserved
                        ? AgentCapabilityFactValue.Supported
                        : AgentCapabilityFactValue.Unknown)
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateRawTraceRow() =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.RawTrace,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.NotSupported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.NotSupported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));

    private static AgentCapabilityState ResolveIdeContextState(
        bool transportReady,
        bool contextManifestPresent)
    {
        if (!transportReady)
        {
            return AdvertisedButUnavailableRow();
        }

        if (!contextManifestPresent)
        {
            return AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Supported,
                configured: AgentCapabilityFactValue.NotSupported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported);
        }

        return AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Supported,
            configured: AgentCapabilityFactValue.Supported,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.Supported);
    }

    private static AgentCapabilityRow CreateUnavailableRow(AgentCapabilityId capabilityId) =>
        AgentCapabilityRow.Create(capabilityId, AdvertisedButUnavailableRow());

    private static AgentCapabilityState SupportedUsableRow() =>
        AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Supported,
            configured: AgentCapabilityFactValue.Supported,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.Supported);

    private static AgentCapabilityState AdvertisedButUnavailableRow() =>
        AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Unavailable,
            configured: AgentCapabilityFactValue.Unavailable,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.Unavailable);
}
