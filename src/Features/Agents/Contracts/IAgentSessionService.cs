using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
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

    /// <summary>
    /// Explicitly ends live session ownership for <paramref name="conversationId"/>.
    /// Emits termination intent, waits a bounded time for local/backend acknowledgement,
    /// and on success removes ownership so a later send creates a fresh session.
    /// Never claims provider deletion or remote process termination without evidence.
    /// </summary>
    Task<AgentSessionEndResult> EndAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);

    AgentSessionSnapshot? TryGetSessionSnapshot(ConversationId conversationId);

    AgentRunSnapshot? TryGetActiveRunSnapshot(ConversationId conversationId);

    AgentSessionContinuityReconcileSummary ReconcileInterruptedSessions(
        AgentSessionContinuityReconcileRequest request);

    AgentSessionContinuityOperationResult ResumeInterruptedSession(
        AgentSessionContinuityResumeRequest request);

    AgentSessionContinuityOperationResult TerminateInterruptedSession(
        AgentSessionContinuityTerminateRequest request);
}
