using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Read-only observation of one Agent Session at a point in time.
/// </summary>
internal sealed class AgentSessionSnapshot
{
    public AgentSessionSnapshot(
        AgentSessionId sessionId,
        ConversationId conversationId,
        ActorId agentIdentity,
        AgentBackendId backendId,
        string backendVersion,
        AgentSessionStatus status,
        AgentCapabilitySnapshot capabilitySnapshot,
        ExecutionRunId? activeRunId)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (agentIdentity == default)
        {
            throw new ArgumentException("Agent identity is required.", nameof(agentIdentity));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (string.IsNullOrWhiteSpace(backendVersion))
        {
            throw new ArgumentException("Backend version is required.", nameof(backendVersion));
        }

        ArgumentNullException.ThrowIfNull(capabilitySnapshot);
        if (capabilitySnapshot.BackendId != backendId)
        {
            throw new ArgumentException(
                "Capability snapshot backend id must match session backend id.",
                nameof(capabilitySnapshot));
        }

        ValidateStatusAndActiveRun(status, activeRunId);

        SessionId = sessionId;
        ConversationId = conversationId;
        AgentIdentity = agentIdentity;
        BackendId = backendId;
        BackendVersion = backendVersion;
        Status = status;
        CapabilitySnapshot = capabilitySnapshot;
        ActiveRunId = activeRunId;
    }

    public AgentSessionId SessionId { get; }

    public ConversationId ConversationId { get; }

    public ActorId AgentIdentity { get; }

    public AgentBackendId BackendId { get; }

    public string BackendVersion { get; }

    public AgentSessionStatus Status { get; }

    public AgentCapabilitySnapshot CapabilitySnapshot { get; }

    public ExecutionRunId? ActiveRunId { get; }

    private static void ValidateStatusAndActiveRun(AgentSessionStatus status, ExecutionRunId? activeRunId)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Session status is invalid.");
        }

        if (activeRunId is { } runId && runId == default)
        {
            throw new ArgumentException(
                "Active run id cannot be default.",
                nameof(activeRunId));
        }

        var hasActiveRun = activeRunId is not null;

        switch (status)
        {
            case AgentSessionStatus.Ready:
            case AgentSessionStatus.Ended:
                if (hasActiveRun)
                {
                    throw new ArgumentException(
                        "Active run id is not allowed for this session status.",
                        nameof(activeRunId));
                }

                break;
            case AgentSessionStatus.Running:
                if (!hasActiveRun)
                {
                    throw new ArgumentException(
                        "Active run id is required when session status is Running.",
                        nameof(activeRunId));
                }

                break;
        }
    }
}
