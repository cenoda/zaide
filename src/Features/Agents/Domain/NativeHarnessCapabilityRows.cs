using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Canonical six-fact capability rows for the Native Harness backend identity.
/// </summary>
internal static class NativeHarnessCapabilityRows
{
    public static AgentCapabilitySnapshot CreateInitialSnapshot(
        bool providerConfigured,
        bool workspaceCaptured,
        bool contextManifestPresent,
        bool streamingSupportedByProvider)
    {
        return AgentCapabilitySnapshot.CreateInitial(
            AgentBackendIds.NativeHarness,
            new[]
            {
                CreateMessageCompletionRow(providerConfigured),
                CreateToolsRow(providerConfigured, workspaceCaptured),
                CreatePermissionsRow(providerConfigured, workspaceCaptured),
                CreateIdeContextRow(providerConfigured, contextManifestPresent),
                CreateStreamingRow(providerConfigured, streamingSupportedByProvider),
                CreateCancellationRow(providerConfigured),
            },
            version: 1);
    }

    public static AgentCapabilityRow CreateMessageCompletionRow(bool providerConfigured) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            providerConfigured
                ? SupportedUsableRow()
                : AdvertisedButUnavailableRow());

    public static AgentCapabilityRow CreateToolsRow(bool providerConfigured, bool workspaceCaptured) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Tools,
            ResolveToolsState(providerConfigured, workspaceCaptured));

    public static AgentCapabilityRow CreatePermissionsRow(bool providerConfigured, bool workspaceCaptured) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Permissions,
            ResolvePermissionsState(providerConfigured, workspaceCaptured));

    public static AgentCapabilityRow CreateIdeContextRow(
        bool providerConfigured,
        bool contextManifestPresent) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.IdeContext,
            ResolveIdeContextState(providerConfigured, contextManifestPresent));

    public static AgentCapabilityRow CreateStreamingRow(
        bool providerConfigured,
        bool streamingSupportedByProvider) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            ResolveStreamingState(providerConfigured, streamingSupportedByProvider));

    public static AgentCapabilityRow CreateCancellationRow(bool providerConfigured) =>
        AgentCapabilityRow.Create(
            AgentCapabilityId.Cancellation,
            providerConfigured
                ? SupportedUsableRow()
                : AdvertisedButUnavailableRow());

    private static AgentCapabilityState ResolveToolsState(bool providerConfigured, bool workspaceCaptured)
    {
        if (!providerConfigured)
        {
            return AdvertisedButUnavailableRow();
        }

        if (!workspaceCaptured)
        {
            return AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Unavailable,
                configured: AgentCapabilityFactValue.Supported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Unavailable);
        }

        return AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Supported,
            configured: AgentCapabilityFactValue.Supported,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.Unknown);
    }

    private static AgentCapabilityState ResolvePermissionsState(bool providerConfigured, bool workspaceCaptured)
    {
        if (!providerConfigured || !workspaceCaptured)
        {
            return AdvertisedButUnavailableRow();
        }

        return AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Supported,
            configured: AgentCapabilityFactValue.Supported,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.Unknown);
    }

    private static AgentCapabilityState ResolveIdeContextState(
        bool providerConfigured,
        bool contextManifestPresent)
    {
        if (!providerConfigured)
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

    private static AgentCapabilityState ResolveStreamingState(
        bool providerConfigured,
        bool streamingSupportedByProvider)
    {
        if (!providerConfigured)
        {
            return AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Unavailable,
                configured: AgentCapabilityFactValue.Unavailable,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Unavailable);
        }

        if (!streamingSupportedByProvider)
        {
            return AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.Supported,
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
