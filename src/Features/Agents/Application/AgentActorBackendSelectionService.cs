using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Application service for explicit actor/backend selection and bounded auth state.
/// Durable mutations go through the typed binding store; runtime auth/method
/// caches are cleared on successful update/unbind.
/// </summary>
internal sealed class AgentActorBackendSelectionService : IAgentActorBackendSelectionService
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly Dictionary<ActorId, IReadOnlyList<string>> _advertisedAuthMethods = new();
    private readonly object _sync = new();
    private readonly List<Action<AgentActorBackendBindingChangedEvent>> _changeHandlers = new();

    public AgentActorBackendSelectionService(IAgentActorBackendBindingStore bindingStore)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _bindingStore.BindingChanged += OnStoreBindingChanged;
    }

    public event Action<AgentActorBackendBindingChangedEvent>? BindingChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Add(value);
            }
        }
        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Remove(value);
            }
        }
    }

    public AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId)
    {
        if (!_bindingStore.TryGetBinding(actorId, out var binding))
        {
            return new AgentActorBackendBindingSnapshot(
                actorId,
                isBound: false,
                default,
                backendLabel: "Unbound",
                statusCaption: "Select a backend before sending.",
                isDisconnected: false,
                AgentAuthenticationConnectionState.Disconnected,
                Array.Empty<string>());
        }

        var backendLabel = ResolveBackendLabel(binding.BackendId);
        var authMethods = GetAdvertisedAuthMethodIds(actorId);
        var statusCaption = BuildStatusCaption(binding, authMethods);
        var isDisconnected = binding.BackendId == AgentBackendIds.Acp
                             && binding.AuthenticationState == AgentAuthenticationConnectionState.Failed;

        return new AgentActorBackendBindingSnapshot(
            actorId,
            isBound: true,
            binding.BackendId,
            backendLabel,
            statusCaption,
            isDisconnected,
            binding.AuthenticationState,
            authMethods);
    }

    public void BindNativeHarness(ActorId actorId) =>
        _ = TryBindNativeHarness(actorId);

    public void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion) =>
        _ = TryBindAcpRuntime(actorId, runtimeIdentity, expectedAgentName, expectedAgentVersion);

    public AgentActorBackendBindingMutationResult TryBindNativeHarness(ActorId actorId)
    {
        var candidate = new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness,
            authenticationState: AgentAuthenticationConnectionState.NotRequired);

        return _bindingStore.TryBind(candidate);
    }

    public AgentActorBackendBindingMutationResult TryBindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);

        try
        {
            var candidate = new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                runtimeIdentity,
                expectedAgentName,
                expectedAgentVersion,
                authenticationState: AgentAuthenticationConnectionState.Disconnected);

            var result = _bindingStore.TryBind(candidate);
            if (result.IsSuccess)
            {
                ClearAdvertisedAuthMethods(actorId);
            }

            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Bind,
                actorId,
                _bindingStore.GetRevision(actorId),
                ex.Message);
        }
    }

    public AgentActorBackendBindingMutationResult TryUpdateNativeHarness(
        ActorId actorId,
        long expectedRevision)
    {
        var candidate = new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness,
            authenticationState: AgentAuthenticationConnectionState.NotRequired);

        var result = _bindingStore.TryUpdate(actorId, candidate, expectedRevision);
        if (result.IsSuccess)
        {
            ClearAdvertisedAuthMethods(actorId);
        }

        return result;
    }

    public AgentActorBackendBindingMutationResult TryUpdateAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion,
        long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);

        try
        {
            var candidate = new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                runtimeIdentity,
                expectedAgentName,
                expectedAgentVersion,
                authenticationState: AgentAuthenticationConnectionState.Disconnected);

            var result = _bindingStore.TryUpdate(actorId, candidate, expectedRevision);
            if (result.IsSuccess)
            {
                // Idle durable update invalidates cached runtime auth/capability state.
                ClearAdvertisedAuthMethods(actorId);
            }

            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Update,
                actorId,
                _bindingStore.GetRevision(actorId),
                ex.Message);
        }
    }

    public AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId, long expectedRevision)
    {
        var result = _bindingStore.TryUnbind(actorId, expectedRevision);
        if (result.IsSuccess)
        {
            ClearAdvertisedAuthMethods(actorId);
        }

        return result;
    }

    public IReadOnlyList<string> GetAdvertisedAuthMethodIds(ActorId actorId)
    {
        lock (_sync)
        {
            return _advertisedAuthMethods.TryGetValue(actorId, out var methods)
                ? methods
                : Array.Empty<string>();
        }
    }

    internal void RecordAdvertisedAuthMethods(ActorId actorId, IReadOnlyList<string> methodIds)
    {
        lock (_sync)
        {
            _advertisedAuthMethods[actorId] = methodIds;
        }
    }

    public Task RequestAuthenticateAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(methodId))
        {
            throw new ArgumentException("Authentication method id is required.", nameof(methodId));
        }

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp)
        {
            throw new InvalidOperationException("ACP authentication requires an explicit ACP binding.");
        }

        var advertised = GetAdvertisedAuthMethodIds(actorId);
        if (advertised.Count > 0
            && !advertised.Any(method => string.Equals(method, methodId, StringComparison.Ordinal)))
        {
            // Runtime-only auth state rewrite; not a durable identity mutation.
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                methodId,
                AgentAuthenticationConnectionState.Failed);
            throw new InvalidOperationException("Authentication method is not advertised by the agent.");
        }

        _bindingStore.SetRuntimeAuthentication(
            actorId,
            methodId,
            AgentAuthenticationConnectionState.Authenticated);

        return Task.CompletedTask;
    }

    private void ClearAdvertisedAuthMethods(ActorId actorId)
    {
        lock (_sync)
        {
            _advertisedAuthMethods.Remove(actorId);
        }
    }

    private void OnStoreBindingChanged(AgentActorBackendBindingChangedEvent change)
    {
        if (change.Kind is AgentActorBackendBindingMutationKind.Update
            or AgentActorBackendBindingMutationKind.Unbind)
        {
            ClearAdvertisedAuthMethods(change.ActorId);
        }

        Action<AgentActorBackendBindingChangedEvent>[] handlers;
        lock (_sync)
        {
            handlers = _changeHandlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            handler(change);
        }
    }

    private static string ResolveBackendLabel(AgentBackendId backendId) =>
        backendId == AgentBackendIds.Acp
            ? "ACP"
            : backendId == AgentBackendIds.NativeHarness
                ? "Native Harness"
                : backendId.Value;

    private static string BuildStatusCaption(
        AgentActorBackendBinding binding,
        IReadOnlyList<string> authMethods)
    {
        if (binding.BackendId == AgentBackendIds.NativeHarness)
        {
            return "Backend: Native Harness";
        }

        if (binding.BackendId != AgentBackendIds.Acp)
        {
            return $"Backend: {binding.BackendId.Value}";
        }

        var runtime = binding.AcpRuntime!;
        var caption = $"Backend: ACP ({runtime.ExecutablePath})";
        if (authMethods.Count == 0)
        {
            return $"{caption} · Auth: not required";
        }

        return binding.AuthenticationState switch
        {
            AgentAuthenticationConnectionState.Authenticated =>
                $"{caption} · Auth: {binding.SelectedAuthMethodId}",
            AgentAuthenticationConnectionState.Failed =>
                $"{caption} · Auth: failed",
            AgentAuthenticationConnectionState.PendingUserAction =>
                $"{caption} · Auth: select method",
            _ => $"{caption} · Auth: disconnected",
        };
    }
}
