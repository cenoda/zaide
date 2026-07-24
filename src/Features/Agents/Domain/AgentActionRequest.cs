using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable admitted action request with identity bundle, fingerprint, and display summary.
/// </summary>
internal sealed class AgentActionRequest
{
    public AgentActionRequest(
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        AgentActionPayload payload,
        AgentActionRequestFingerprint fingerprint,
        AgentActionDisplaySummary displaySummary)
    {
        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (attemptId == default)
        {
            throw new ArgumentException("Attempt id is required.", nameof(attemptId));
        }

        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (workspaceIdentity == default)
        {
            throw new ArgumentException("Workspace identity is required.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration == default)
        {
            throw new ArgumentException("Workspace generation is required.", nameof(workspaceGeneration));
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!AgentActionPayload.MatchesKind(payload.Kind, payload))
        {
            throw new ArgumentException("Action payload kind is inconsistent.", nameof(payload));
        }

        if (fingerprint == default)
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(fingerprint));
        }

        ArgumentNullException.ThrowIfNull(displaySummary);
        if (displaySummary.Kind != payload.Kind)
        {
            throw new ArgumentException(
                "Display summary kind must match payload kind.",
                nameof(displaySummary));
        }

        ActionId = actionId;
        AttemptId = attemptId;
        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
        BackendId = backendId;
        WorkspaceIdentity = workspaceIdentity;
        WorkspaceGeneration = workspaceGeneration;
        Payload = payload;
        Fingerprint = fingerprint;
        DisplaySummary = displaySummary;
    }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    public AgentBackendId BackendId { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceGeneration WorkspaceGeneration { get; }

    public AgentActionPayload Payload { get; }

    public AgentActionRequestFingerprint Fingerprint { get; }

    public AgentActionDisplaySummary DisplaySummary { get; }
}
