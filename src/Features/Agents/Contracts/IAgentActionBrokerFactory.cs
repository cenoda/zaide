using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Creates run-scoped action brokers bound to authoritative session/run state.
/// </summary>
internal interface IAgentActionBrokerFactory
{
    IAgentActionBroker CreateRunScopedBroker(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        IAgentActionEventPublisher eventPublisher);
}
