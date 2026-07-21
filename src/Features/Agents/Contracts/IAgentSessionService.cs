using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Backend-neutral Agent Session application boundary. Implementations own
/// lifecycle truth, event ordering, and read-only session/run observations.
/// </summary>
internal interface IAgentSessionService
{
    IObservable<AgentEvent> Events { get; }

    Task<AgentRunSnapshot> SendAsync(
        ConversationId conversationId,
        ActorId initiatorActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        ConversationEntryId messageEntryId,
        string messageText,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task EndAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    AgentSessionSnapshot? TryGetSessionSnapshot(ConversationId conversationId);

    AgentRunSnapshot? TryGetActiveRunSnapshot(ConversationId conversationId);
}
