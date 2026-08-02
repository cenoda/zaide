using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Explicit user-driven backend and authentication selection boundary.
/// </summary>
internal interface IAgentActorBackendSelectionService
{
    AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId);

    /// <summary>
    /// Compatibility wrapper used by existing presenters/tests. Prefer
    /// <see cref="TryBindNativeHarness"/> for truthful mutation outcomes.
    /// </summary>
    void BindNativeHarness(ActorId actorId);

    /// <summary>
    /// Compatibility wrapper used by existing presenters/tests. Prefer
    /// <see cref="TryBindAcpRuntime"/> for truthful mutation outcomes.
    /// </summary>
    void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion);

    AgentActorBackendBindingMutationResult TryBindNativeHarness(ActorId actorId);

    AgentActorBackendBindingMutationResult TryBindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion);

    AgentActorBackendBindingMutationResult TryUpdateNativeHarness(
        ActorId actorId,
        long expectedRevision);

    AgentActorBackendBindingMutationResult TryUpdateAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion,
        long expectedRevision);

    AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId, long expectedRevision);

    IReadOnlyList<string> GetAdvertisedAuthMethodIds(ActorId actorId);

    event Action<AgentActorBackendBindingChangedEvent>? BindingChanged;

    Task RequestAuthenticateAsync(ActorId actorId, string methodId, CancellationToken cancellationToken);
}
