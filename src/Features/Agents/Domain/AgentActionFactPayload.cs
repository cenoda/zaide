using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed payload for Phase 17 action and permission facts emitted through the
/// Phase 15 event stream.
/// </summary>
/// <remarks>
/// Workspace attribution is optional: when no workspace scope was captured
/// (for example <see cref="AgentActionFailureKind.NoWorkspace"/> early denials),
/// both workspace fields are null. When a workspace was captured, both fields
/// carry the exact captured identity and generation — never a fabricated value.
/// </remarks>
internal sealed class AgentActionFactPayload : AgentEventPayload
{
    public AgentActionFactPayload(
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentActionKind actionKind,
        ActorId initiatingActorId,
        ActorId targetActorId,
        WorkspaceIdentity? workspaceIdentity,
        WorkspaceGeneration? workspaceGeneration,
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

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (workspaceIdentity is null != workspaceGeneration is null)
        {
            throw new ArgumentException(
                "Workspace identity and generation must both be present or both absent.");
        }

        if (workspaceIdentity is { } identity && identity == default)
        {
            throw new ArgumentException("Workspace identity is invalid.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration is { } generation && generation == default)
        {
            throw new ArgumentException("Workspace generation is invalid.", nameof(workspaceGeneration));
        }

        ArgumentNullException.ThrowIfNull(summary);

        ActionId = actionId;
        AttemptId = attemptId;
        ActionKind = actionKind;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
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

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    /// <summary>
    /// Captured workspace identity, or <c>null</c> when no workspace scope was captured.
    /// </summary>
    public WorkspaceIdentity? WorkspaceIdentity { get; }

    /// <summary>
    /// Captured workspace generation, or <c>null</c> when no workspace scope was captured.
    /// </summary>
    public WorkspaceGeneration? WorkspaceGeneration { get; }

    public AgentActionAuditSummary Summary { get; }

    public AgentActionPermissionClassification? Classification { get; }

    public AgentPermissionDecisionStatus? DecisionStatus { get; }

    public bool? DecisionIsAllow { get; }

    public AgentActionResultKind? ResultKind { get; }

    public AgentActionFailureKind? FailureKind { get; }

    public AgentDocumentReconciliationOutcome? ReconciliationOutcome { get; }
}
