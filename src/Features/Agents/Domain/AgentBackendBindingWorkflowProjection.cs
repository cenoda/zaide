using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// User-facing backend binding projection for Townhall. Distinguishes bound vs
/// unbound, provider configuration, workspace capture, and context-manifest
/// presence without equating binding with network success or entitlement.
/// </summary>
internal sealed class AgentBackendBindingWorkflowProjection
{
    public AgentBackendBindingWorkflowProjection(
        ActorId actorId,
        bool isBound,
        AgentBackendId backendId,
        string backendLabel,
        string statusCaption,
        string authCaption,
        bool isDisconnected,
        AgentAuthenticationConnectionState authenticationState,
        long revision,
        bool providerConfigured,
        bool workspaceCaptured,
        bool contextManifestPresent,
        string capabilityCaption,
        string settingsCaption,
        string? mutationErrorCaption,
        bool canBindNativeHarness,
        bool canUnbind,
        IReadOnlyList<string> advertisedAuthMethodIds,
        string? acpExecutablePath = null,
        string? acpArgumentsCaption = null,
        string? acpExpectedAgentName = null,
        string? acpExpectedAgentVersion = null,
        bool canProbeAcp = false,
        bool canAuthenticate = false,
        bool canLogout = false,
        string? acpRuntimeCaption = null)
    {
        ActorId = actorId;
        IsBound = isBound;
        BackendId = backendId;
        BackendLabel = backendLabel ?? throw new ArgumentNullException(nameof(backendLabel));
        StatusCaption = statusCaption ?? throw new ArgumentNullException(nameof(statusCaption));
        AuthCaption = authCaption ?? throw new ArgumentNullException(nameof(authCaption));
        IsDisconnected = isDisconnected;
        AuthenticationState = authenticationState;
        Revision = revision;
        ProviderConfigured = providerConfigured;
        WorkspaceCaptured = workspaceCaptured;
        ContextManifestPresent = contextManifestPresent;
        CapabilityCaption = capabilityCaption ?? throw new ArgumentNullException(nameof(capabilityCaption));
        SettingsCaption = settingsCaption ?? throw new ArgumentNullException(nameof(settingsCaption));
        MutationErrorCaption = mutationErrorCaption;
        CanBindNativeHarness = canBindNativeHarness;
        CanUnbind = canUnbind;
        AdvertisedAuthMethodIds = advertisedAuthMethodIds
            ?? throw new ArgumentNullException(nameof(advertisedAuthMethodIds));
        AcpExecutablePath = acpExecutablePath;
        AcpArgumentsCaption = acpArgumentsCaption;
        AcpExpectedAgentName = acpExpectedAgentName;
        AcpExpectedAgentVersion = acpExpectedAgentVersion;
        CanProbeAcp = canProbeAcp;
        CanAuthenticate = canAuthenticate;
        CanLogout = canLogout;
        AcpRuntimeCaption = acpRuntimeCaption;
    }

    public ActorId ActorId { get; }

    public bool IsBound { get; }

    public AgentBackendId BackendId { get; }

    public string BackendLabel { get; }

    public string StatusCaption { get; }

    public string AuthCaption { get; }

    public bool IsDisconnected { get; }

    public AgentAuthenticationConnectionState AuthenticationState { get; }

    public long Revision { get; }

    public bool ProviderConfigured { get; }

    public bool WorkspaceCaptured { get; }

    public bool ContextManifestPresent { get; }

    public string CapabilityCaption { get; }

    public string SettingsCaption { get; }

    public string? MutationErrorCaption { get; }

    public bool CanBindNativeHarness { get; }

    public bool CanUnbind { get; }

    public IReadOnlyList<string> AdvertisedAuthMethodIds { get; }

    public string? AcpExecutablePath { get; }

    public string? AcpArgumentsCaption { get; }

    public string? AcpExpectedAgentName { get; }

    public string? AcpExpectedAgentVersion { get; }

    public bool CanProbeAcp { get; }

    public bool CanAuthenticate { get; }

    public bool CanLogout { get; }

    public string? AcpRuntimeCaption { get; }

    public static string NativeSettingsCaption { get; } =
        "Configure base URL, model, and API key in Settings. Secrets are never entered in this panel.";

    public static string AcpSecretsCaption { get; } =
        "ACP credentials belong to the ACP agent. Do not enter secrets as launch arguments.";
}
