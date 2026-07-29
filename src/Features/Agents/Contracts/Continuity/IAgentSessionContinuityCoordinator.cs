using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts.Continuity;

internal interface IAgentSessionContinuityCoordinator
{
    AgentSessionContinuityReconcileSummary Reconcile(AgentSessionContinuityReconcileRequest request);

    AgentSessionContinuityOperationResult Resume(AgentSessionContinuityResumeRequest request);

    AgentSessionContinuityOperationResult Terminate(AgentSessionContinuityTerminateRequest request);

    bool TryGetResumedSessionId(
        ConversationId conversationId,
        out AgentSessionId sessionId);

    void RecordCheckpoint(AgentSessionContinuityCheckpoint checkpoint);

    void CheckpointActiveSessions(string workspaceRoot);

    bool TryGetActiveScope(
        ConversationId conversationId,
        out AgentSessionContinuityScope scope);
}
