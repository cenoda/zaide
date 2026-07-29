using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityTerminateRequest
{
    public AgentSessionContinuityTerminateRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        ConversationId conversationId,
        AgentSessionId sessionId,
        ActorId actorId,
        AgentBackendId backendId,
        string idempotencyKey,
        AgentSessionContinuityOperationKind terminationKind =
            AgentSessionContinuityOperationKind.Terminate)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (actorId == default)
        {
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (terminationKind is not (
            AgentSessionContinuityOperationKind.Terminate
            or AgentSessionContinuityOperationKind.Abandon
            or AgentSessionContinuityOperationKind.Archive))
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminationKind),
                terminationKind,
                "Termination kind must be Terminate, Abandon, or Archive.");
        }

        WorkspaceKey = workspaceKey;
        WorkspaceRoot = workspaceRoot;
        ConversationId = conversationId;
        SessionId = sessionId;
        ActorId = actorId;
        BackendId = backendId;
        IdempotencyKey = idempotencyKey;
        TerminationKind = terminationKind;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string WorkspaceRoot { get; }

    public ConversationId ConversationId { get; }

    public AgentSessionId SessionId { get; }

    public ActorId ActorId { get; }

    public AgentBackendId BackendId { get; }

    public string IdempotencyKey { get; }

    public AgentSessionContinuityOperationKind TerminationKind { get; }
}
