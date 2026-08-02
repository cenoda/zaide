using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Production-owned Townhall workflow surface for per-actor backend binding.
/// Surfaces typed mutation results and truthful capability projection.
/// </summary>
internal sealed class AgentBackendBindingPresenter
{
    private readonly IAgentActorBackendSelectionService _selectionService;
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly INativeHarnessProviderOptionsSource? _optionsSource;
    private readonly IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly IAcpOnboardingConnectionService? _onboarding;
    private readonly Dictionary<ActorId, string> _mutationErrors = new();
    private readonly object _sync = new();

    public AgentBackendBindingPresenter(
        IAgentActorBackendSelectionService selectionService,
        IAgentActorBackendBindingStore bindingStore,
        INativeHarnessProviderOptionsSource? optionsSource = null,
        IWorkspaceActionAuthority? workspaceAuthority = null,
        IAcpOnboardingConnectionService? onboarding = null)
    {
        _selectionService = selectionService
            ?? throw new ArgumentNullException(nameof(selectionService));
        _bindingStore = bindingStore
            ?? throw new ArgumentNullException(nameof(bindingStore));
        _optionsSource = optionsSource;
        _workspaceAuthority = workspaceAuthority;
        _onboarding = onboarding;
        _selectionService.BindingChanged += OnSelectionBindingChanged;
    }

    public event Action<AgentActorBackendBindingChangedEvent>? BindingChanged;

    public AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId) =>
        _selectionService.GetSnapshot(actorId);

    public AgentBackendBindingWorkflowProjection BuildProjection(ActorId actorId)
    {
        var snapshot = _selectionService.GetSnapshot(actorId);
        var revision = _bindingStore.GetRevision(actorId);
        var providerConfigured = ResolveProviderConfigured();
        var workspaceCaptured = _workspaceAuthority?.TryCaptureCurrentScope(out _) == true;
        // Context-manifest presence is a separate truth; binding does not invent it.
        // Honest default: not present until an admitted run captures IDE context.
        var contextManifestPresent = false;

        string? mutationError;
        lock (_sync)
        {
            _mutationErrors.TryGetValue(actorId, out mutationError);
        }

        var capabilityCaption = BuildCapabilityCaption(
            snapshot,
            providerConfigured,
            workspaceCaptured,
            contextManifestPresent);

        string? acpExecutable = null;
        string? acpArgsCaption = null;
        string? acpName = null;
        string? acpVersion = null;
        string? acpRuntimeCaption = null;
        if (snapshot.IsBound
            && snapshot.BackendId == AgentBackendIds.Acp
            && _bindingStore.TryGetBinding(actorId, out var binding)
            && binding.AcpRuntime is not null)
        {
            acpExecutable = binding.AcpRuntime.ExecutablePath;
            acpArgsCaption = binding.AcpRuntime.Arguments.Count == 0
                ? "(no arguments)"
                : string.Join(' ', binding.AcpRuntime.Arguments);
            acpName = binding.ExpectedAgentName;
            acpVersion = binding.ExpectedAgentVersion;
            acpRuntimeCaption =
                $"{binding.AcpRuntime.ExecutablePath} · expected {binding.ExpectedAgentName} {binding.ExpectedAgentVersion}";
        }

        var isNativeBound = snapshot.IsBound && snapshot.BackendId == AgentBackendIds.NativeHarness;
        var canBindNative = !snapshot.IsBound || snapshot.BackendId != AgentBackendIds.NativeHarness;
        var canUnbind = snapshot.IsBound;
        var isAcpBound = snapshot.IsBound && snapshot.BackendId == AgentBackendIds.Acp;
        // Show ACP config when unbound (bind path) or ACP-bound (reconfigure).
        // Hide when Native Harness is the active binding to avoid clutter.
        var showAcpConfig = !snapshot.IsBound || isAcpBound;
        var logoutSupported = isAcpBound
            && _onboarding?.IsLogoutSupported(actorId) == true
            && snapshot.AuthenticationState == AgentAuthenticationConnectionState.Authenticated;

        return new AgentBackendBindingWorkflowProjection(
            actorId,
            snapshot.IsBound,
            snapshot.BackendId,
            snapshot.BackendLabel,
            statusCaption: snapshot.StatusCaption,
            authCaption: FormatAuthCaption(snapshot.AuthenticationState),
            isDisconnected: snapshot.IsDisconnected,
            authenticationState: snapshot.AuthenticationState,
            revision: revision,
            providerConfigured: providerConfigured,
            workspaceCaptured: workspaceCaptured,
            contextManifestPresent: contextManifestPresent,
            capabilityCaption: capabilityCaption,
            settingsCaption: isNativeBound || !snapshot.IsBound
                ? AgentBackendBindingWorkflowProjection.NativeSettingsCaption
                : AgentBackendBindingWorkflowProjection.AcpSecretsCaption,
            mutationErrorCaption: mutationError,
            canBindNativeHarness: canBindNative,
            canUnbind: canUnbind,
            advertisedAuthMethodIds: snapshot.AdvertisedAuthMethodIds,
            acpExecutablePath: acpExecutable,
            acpArgumentsCaption: acpArgsCaption,
            acpExpectedAgentName: acpName,
            acpExpectedAgentVersion: acpVersion,
            canProbeAcp: isAcpBound,
            canAuthenticate: isAcpBound
                && snapshot.AdvertisedAuthMethodIds.Count > 0
                && snapshot.AuthenticationState != AgentAuthenticationConnectionState.Authenticated,
            canLogout: logoutSupported,
            acpRuntimeCaption: acpRuntimeCaption,
            showAcpConfig: showAcpConfig);
    }

    /// <summary>
    /// Binds Native Harness for the actor (TryBind when unbound; TryUpdate when bound).
    /// </summary>
    public AgentActorBackendBindingMutationResult TryBindNativeHarness(ActorId actorId)
    {
        var result = _selectionService.TryBindNativeHarness(actorId);
        RecordMutationOutcome(actorId, result);
        return result;
    }

    /// <summary>
    /// Compatibility void wrapper retained for older tests; prefer typed methods.
    /// </summary>
    public void BindNativeHarness(ActorId actorId) =>
        _ = TryBindNativeHarness(actorId);

    public AgentActorBackendBindingMutationResult TryBindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        var result = _selectionService.TryBindAcpRuntime(
            actorId,
            runtimeIdentity,
            expectedAgentName,
            expectedAgentVersion);
        RecordMutationOutcome(actorId, result);
        return result;
    }

    /// <summary>
    /// Compatibility void wrapper retained for older tests; prefer typed methods.
    /// </summary>
    public void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion) =>
        _ = TryBindAcpRuntime(actorId, runtimeIdentity, expectedAgentName, expectedAgentVersion);

    public AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId)
    {
        if (!_bindingStore.TryGetBinding(actorId, out var existing))
        {
            var unbound = AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Unbind,
                actorId,
                currentRevision: 0,
                "Actor is already unbound.");
            RecordMutationOutcome(actorId, unbound);
            return unbound;
        }

        var result = _selectionService.TryUnbind(actorId, existing.Revision);
        RecordMutationOutcome(actorId, result);
        return result;
    }

    public void ClearMutationError(ActorId actorId)
    {
        lock (_sync)
        {
            _mutationErrors.Remove(actorId);
        }
    }

    public async Task<AcpOnboardingProbeResult> ProbeAcpAsync(
        ActorId actorId,
        CancellationToken cancellationToken = default)
    {
        if (_onboarding is null)
        {
            var missing = AcpOnboardingProbeResult.Failed(
                actorId,
                "ACP onboarding connection service is not available.");
            RecordMutationMessage(actorId, missing.Message);
            return missing;
        }

        var result = await _onboarding.ProbeAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            ClearMutationError(actorId);
        }
        else
        {
            RecordMutationMessage(actorId, result.Message);
        }

        return result;
    }

    public async Task<AcpOnboardingAuthResult> AuthenticateAcpAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken = default)
    {
        if (_onboarding is null)
        {
            var missing = AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP onboarding connection service is not available.",
                methodId);
            RecordMutationMessage(actorId, missing.Message);
            return missing;
        }

        var result = await _onboarding.AuthenticateAsync(actorId, methodId, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            ClearMutationError(actorId);
        }
        else
        {
            RecordMutationMessage(actorId, result.Message);
        }

        return result;
    }

    public async Task<AcpOnboardingLogoutResult> LogoutAcpAsync(
        ActorId actorId,
        CancellationToken cancellationToken = default)
    {
        if (_onboarding is null)
        {
            var missing = AcpOnboardingLogoutResult.Failed(
                actorId,
                "ACP onboarding connection service is not available.");
            RecordMutationMessage(actorId, missing.Message);
            return missing;
        }

        var result = await _onboarding.LogoutAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            ClearMutationError(actorId);
        }
        else
        {
            RecordMutationMessage(actorId, result.Message);
        }

        return result;
    }

    private void RecordMutationMessage(ActorId actorId, string? message)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _mutationErrors.Remove(actorId);
            }
            else
            {
                _mutationErrors[actorId] = message;
            }
        }
    }

    private void RecordMutationOutcome(ActorId actorId, AgentActorBackendBindingMutationResult result)
    {
        lock (_sync)
        {
            if (result.IsSuccess)
            {
                _mutationErrors.Remove(actorId);
            }
            else
            {
                _mutationErrors[actorId] = FormatMutationError(result);
            }
        }
    }

    private static string FormatMutationError(AgentActorBackendBindingMutationResult result) =>
        result.Status switch
        {
            AgentActorBackendBindingMutationStatus.Busy =>
                result.Message ?? "Binding is busy: cancel or wait for the active run.",
            AgentActorBackendBindingMutationStatus.Conflict =>
                result.Message ?? "Binding revision conflict: refresh and retry.",
            AgentActorBackendBindingMutationStatus.PersistenceFailed =>
                result.Message ?? "Failed to persist binding. Prior state was kept.",
            AgentActorBackendBindingMutationStatus.ValidationFailed =>
                result.Message ?? "Binding validation failed.",
            AgentActorBackendBindingMutationStatus.RecoveryRequired =>
                result.Message ?? "Binding store recovery is required.",
            _ => result.Message ?? "Binding mutation failed.",
        };

    private bool ResolveProviderConfigured()
    {
        var options = _optionsSource?.ResolveOptions();
        return options is not null
            && Application.NativeHarnessProviderConfigured.IsConfigured(options);
    }

    private static string BuildCapabilityCaption(
        AgentActorBackendBindingSnapshot snapshot,
        bool providerConfigured,
        bool workspaceCaptured,
        bool contextManifestPresent)
    {
        if (!snapshot.IsBound)
        {
            return "Unbound · select Native Harness or ACP before sending.";
        }

        if (snapshot.BackendId == AgentBackendIds.NativeHarness)
        {
            var rows = NativeHarnessCapabilityRows.CreateInitialSnapshot(
                providerConfigured,
                workspaceCaptured,
                contextManifestPresent,
                streamingSupportedByProvider: true);

            var completion = rows.Rows.First(r => r.CapabilityId == AgentCapabilityId.MessageCompletion);
            var usable = completion.State.CurrentlyUsable == AgentCapabilityFactValue.Supported;
            return string.Join(
                " · ",
                new[]
                {
                    providerConfigured ? "provider configured" : "provider not configured",
                    workspaceCaptured ? "workspace captured" : "workspace not captured",
                    contextManifestPresent ? "context-manifest present" : "context-manifest absent",
                    usable ? "message completion usable" : "message completion not usable",
                });
        }

        if (snapshot.BackendId == AgentBackendIds.Acp)
        {
            return snapshot.StatusCaption;
        }

        return snapshot.StatusCaption;
    }

    private static string FormatAuthCaption(AgentAuthenticationConnectionState authState) =>
        authState switch
        {
            AgentAuthenticationConnectionState.NotRequired => "Auth not required",
            AgentAuthenticationConnectionState.Authenticated => "Authenticated",
            AgentAuthenticationConnectionState.PendingUserAction => "Authentication required",
            AgentAuthenticationConnectionState.Disconnected => "Disconnected",
            AgentAuthenticationConnectionState.Failed => "Authentication failed",
            _ => authState.ToString(),
        };

    private void OnSelectionBindingChanged(AgentActorBackendBindingChangedEvent change) =>
        BindingChanged?.Invoke(change);
}
