using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Reactive notification published only after a durable mutation succeeds and
/// the in-memory snapshot is committed.
/// </summary>
internal sealed class AgentActorBackendBindingChangedEvent
{
    public AgentActorBackendBindingChangedEvent(
        ActorId actorId,
        AgentActorBackendBindingMutationKind kind,
        long revision,
        bool isBound)
    {
        ActorId = actorId;
        Kind = kind;
        Revision = revision;
        IsBound = isBound;
    }

    public ActorId ActorId { get; }

    public AgentActorBackendBindingMutationKind Kind { get; }

    public long Revision { get; }

    public bool IsBound { get; }
}
