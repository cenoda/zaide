using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Owns explicit per-actor backend/runtime bindings.
/// </summary>
internal interface IAgentActorBackendBindingStore
{
    bool TryGetBinding(ActorId actorId, out AgentActorBackendBinding binding);

    bool HasBinding(ActorId actorId);

    AgentBackendId GetRequiredBackendId(ActorId actorId);

    void SetBinding(AgentActorBackendBinding binding);
}
