using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Composes validated immutable action requests with fingerprints and display summaries.
/// </summary>
internal static class AgentActionRequestComposer
{
    public static AgentActionRequest Compose(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        AgentActionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!AgentActionPayload.MatchesKind(payload.Kind, payload))
        {
            throw new ArgumentException("Action payload kind is inconsistent.", nameof(payload));
        }

        var actionId = AgentActionId.New();
        var attemptId = AgentActionAttemptId.New();
        var displaySummary = AgentActionDisplaySummaryBuilder.Build(payload);
        var fingerprint = AgentActionRequestFingerprintComputer.Compute(
            workspaceIdentity,
            workspaceGeneration,
            runId,
            payload);

        return new AgentActionRequest(
            actionId,
            attemptId,
            sessionId,
            runId,
            conversationId,
            initiatingActorId,
            targetActorId,
            backendId,
            workspaceIdentity,
            workspaceGeneration,
            payload,
            fingerprint,
            displaySummary);
    }
}
