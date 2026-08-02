using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Owns explicit per-actor backend/runtime bindings with durable schema-v1
/// persistence, revisioned mutations, and post-success change notification.
/// </summary>
internal interface IAgentActorBackendBindingStore
{
    bool TryGetBinding(ActorId actorId, out AgentActorBackendBinding binding);

    bool HasBinding(ActorId actorId);

    AgentBackendId GetRequiredBackendId(ActorId actorId);

    long GetRevision(ActorId actorId);

    AgentActorBackendBindingLoadResult LoadResult { get; }

    event Action<AgentActorBackendBindingChangedEvent>? BindingChanged;

    /// <summary>
    /// Compatibility path for existing readers/tests. User/workflow mutations
    /// should use <see cref="TryBind"/> / <see cref="TryUpdate"/> /
    /// <see cref="TryUnbind"/>.
    /// </summary>
    void SetBinding(AgentActorBackendBinding binding);

    /// <summary>
    /// Runtime-only authentication rewrite. Does not advance the durable revision
    /// or rewrite the binding document.
    /// </summary>
    void SetRuntimeAuthentication(
        ActorId actorId,
        string? selectedAuthMethodId,
        AgentAuthenticationConnectionState authenticationState);

    AgentActorBackendBindingMutationResult TryBind(AgentActorBackendBinding binding);

    AgentActorBackendBindingMutationResult TryUpdate(
        ActorId actorId,
        AgentActorBackendBinding binding,
        long expectedRevision);

    AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId, long expectedRevision);
}
