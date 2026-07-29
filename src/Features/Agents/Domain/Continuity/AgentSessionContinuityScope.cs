using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityScope
{
    public AgentSessionContinuityScope(
        ActorId actorId,
        ConversationId conversationId,
        AgentSessionId sessionId,
        ExecutionRunId? runId,
        AgentBackendId backendId,
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot)
    {
        if (actorId == default)
        {
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        ActorId = actorId;
        ConversationId = conversationId;
        SessionId = sessionId;
        RunId = runId;
        BackendId = backendId;
        WorkspaceKey = workspaceKey;
        WorkspaceRoot = workspaceRoot;
    }

    public ActorId ActorId { get; }

    public ConversationId ConversationId { get; }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId? RunId { get; }

    public AgentBackendId BackendId { get; }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string WorkspaceRoot { get; }
}
