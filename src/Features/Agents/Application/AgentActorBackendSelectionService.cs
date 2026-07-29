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
/// </summary>
internal sealed class AgentActorBackendSelectionService : IAgentActorBackendSelectionService
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly Dictionary<ActorId, IReadOnlyList<string>> _advertisedAuthMethods = new();
    private readonly object _sync = new();

    public AgentActorBackendSelectionService(IAgentActorBackendBindingStore bindingStore)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
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

    public void BindNativeHarness(ActorId actorId)
    {
        _bindingStore.SetBinding(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness,
            authenticationState: AgentAuthenticationConnectionState.NotRequired));
    }

    public void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);

        _bindingStore.SetBinding(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.Acp,
            runtimeIdentity,
            expectedAgentName,
            expectedAgentVersion,
            authenticationState: AgentAuthenticationConnectionState.Disconnected));

        lock (_sync)
        {
            _advertisedAuthMethods.Remove(actorId);
        }
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
            _bindingStore.SetBinding(binding.WithAuthentication(
                methodId,
                AgentAuthenticationConnectionState.Failed));
            throw new InvalidOperationException("Authentication method is not advertised by the agent.");
        }

        _bindingStore.SetBinding(binding.WithAuthentication(
            methodId,
            AgentAuthenticationConnectionState.Authenticated));

        return Task.CompletedTask;
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
