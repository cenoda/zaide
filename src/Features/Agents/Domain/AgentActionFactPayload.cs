using System;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed payload for Phase 17 action and permission facts emitted through the
/// Phase 15 event stream.
/// </summary>
internal sealed class AgentActionFactPayload : AgentEventPayload
{
    public AgentActionFactPayload(
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentActionKind actionKind,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        AgentActionAuditSummary summary,
        AgentActionPermissionClassification? classification = null,
        AgentPermissionDecisionStatus? decisionStatus = null,
        bool? decisionIsAllow = null,
        AgentActionResultKind? resultKind = null,
        AgentActionFailureKind? failureKind = null,
        AgentDocumentReconciliationOutcome? reconciliationOutcome = null)
    {
        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (attemptId == default)
        {
            throw new ArgumentException("Attempt id is required.", nameof(attemptId));
        }

        if (!Enum.IsDefined(actionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Action kind is invalid.");
        }

        if (workspaceIdentity == default)
        {
            throw new ArgumentException("Workspace identity is required.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration == default)
        {
            throw new ArgumentException("Workspace generation is required.", nameof(workspaceGeneration));
        }

        ArgumentNullException.ThrowIfNull(summary);

        ActionId = actionId;
        AttemptId = attemptId;
        ActionKind = actionKind;
        WorkspaceIdentity = workspaceIdentity;
        WorkspaceGeneration = workspaceGeneration;
        Summary = summary;
        Classification = classification;
        DecisionStatus = decisionStatus;
        DecisionIsAllow = decisionIsAllow;
        ResultKind = resultKind;
        FailureKind = failureKind;
        ReconciliationOutcome = reconciliationOutcome;
    }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentActionKind ActionKind { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceGeneration WorkspaceGeneration { get; }

    public AgentActionAuditSummary Summary { get; }

    public AgentActionPermissionClassification? Classification { get; }

    public AgentPermissionDecisionStatus? DecisionStatus { get; }

    public bool? DecisionIsAllow { get; }

    public AgentActionResultKind? ResultKind { get; }

    public AgentActionFailureKind? FailureKind { get; }

    public AgentDocumentReconciliationOutcome? ReconciliationOutcome { get; }
}
