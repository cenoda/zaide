using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Failure categories represented by normalized agent events and backend
/// observations for Phase 15.
/// </summary>
internal enum AgentFailureKind
{
    Execution,
    Timeout,
    Transport,
    Cancellation,
    Indeterminate,
}

/// <summary>
/// Base type for one concrete normalized agent event payload.
/// </summary>
internal abstract class AgentEventPayload
{
}

/// <summary>
/// Session lifecycle payload for session status events.
/// </summary>
internal sealed class AgentSessionLifecyclePayload : AgentEventPayload
{
    public AgentSessionLifecyclePayload(AgentSessionStatus status)
    {
        Status = status;
    }

    public AgentSessionStatus Status { get; }
}

/// <summary>
/// Run lifecycle payload for run status events.
/// </summary>
internal sealed class AgentRunLifecyclePayload : AgentEventPayload
{
    public AgentRunLifecyclePayload(AgentRunStatus status)
    {
        Status = status;
    }

    public AgentRunStatus Status { get; }
}

/// <summary>
/// User or assistant message payload for admitted message events.
/// </summary>
internal sealed class AgentMessagePayload : AgentEventPayload
{
    public AgentMessagePayload(ConversationEntryId messageEntryId, string text)
    {
        if (messageEntryId == default)
        {
            throw new ArgumentException("Message entry id is required.", nameof(messageEntryId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Message text is required.", nameof(text));
        }

        MessageEntryId = messageEntryId;
        Text = text;
    }

    public ConversationEntryId MessageEntryId { get; }

    public string Text { get; }
}

/// <summary>
/// Failure payload for normalized failure events.
/// </summary>
internal sealed class AgentFailurePayload : AgentEventPayload
{
    public AgentFailurePayload(AgentFailureKind failureKind, string reason)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Failure kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        FailureKind = failureKind;
        Reason = reason;
    }

    public AgentFailureKind FailureKind { get; }

    public string Reason { get; }
}

/// <summary>
/// Capability snapshot change payload for normalized capability events.
/// </summary>
internal sealed class AgentCapabilityChangedPayload : AgentEventPayload
{
    public AgentCapabilityChangedPayload(AgentCapabilitySnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public AgentCapabilitySnapshot Snapshot { get; }
}

/// <summary>
/// Immutable normalized Agent Session event with exactly one typed payload.
/// </summary>
internal sealed class AgentEvent
{
    public const int CurrentSchemaVersion = 1;

    public AgentEvent(
        AgentEventId eventId,
        int schemaVersion,
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentBackendId backendId,
        long sequence,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset receivedAtUtc,
        AgentEventId? causationEventId,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentEventKind kind,
        AgentEventPayload payload)
    {
        if (eventId == default)
        {
            throw new ArgumentException("Event id is required.", nameof(eventId));
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "Schema version must be positive.");
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

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "Event sequence must be positive.");
        }

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Occurred time is required.", nameof(occurredAtUtc));
        }

        if (receivedAtUtc == default)
        {
            throw new ArgumentException("Received time is required.", nameof(receivedAtUtc));
        }

        if (receivedAtUtc < occurredAtUtc)
        {
            throw new ArgumentException(
                "Received time cannot precede occurred time.",
                nameof(receivedAtUtc));
        }

        if (!Enum.IsDefined(evidenceLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceLevel),
                evidenceLevel,
                "Evidence level is invalid.");
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!PayloadMatchesKind(kind, payload))
        {
            throw new ArgumentException(
                "Event kind and payload type do not match.",
                nameof(payload));
        }

        if (payload is AgentCapabilityChangedPayload capabilityChanged
            && capabilityChanged.Snapshot.BackendId != backendId)
        {
            throw new ArgumentException(
                "Capability snapshot backend id must match event backend id.",
                nameof(payload));
        }

        EventId = eventId;
        SchemaVersion = schemaVersion;
        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        BackendId = backendId;
        Sequence = sequence;
        OccurredAtUtc = occurredAtUtc;
        ReceivedAtUtc = receivedAtUtc;
        CausationEventId = causationEventId;
        EvidenceLevel = evidenceLevel;
        Kind = kind;
        Payload = payload;
    }

    public AgentEventId EventId { get; }

    public int SchemaVersion { get; }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public AgentBackendId BackendId { get; }

    public long Sequence { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public AgentEventId? CausationEventId { get; }

    public AgentActivityEvidenceLevel EvidenceLevel { get; }

    public AgentEventKind Kind { get; }

    public AgentEventPayload Payload { get; }

    private static bool PayloadMatchesKind(AgentEventKind kind, AgentEventPayload payload) =>
        kind switch
        {
            AgentEventKind.SessionReady => payload is AgentSessionLifecyclePayload { Status: AgentSessionStatus.Ready },
            AgentEventKind.SessionRunning => payload is AgentSessionLifecyclePayload { Status: AgentSessionStatus.Running },
            AgentEventKind.SessionEnding => payload is AgentSessionLifecyclePayload { Status: AgentSessionStatus.Ending },
            AgentEventKind.SessionEnded => payload is AgentSessionLifecyclePayload { Status: AgentSessionStatus.Ended },

            AgentEventKind.RunCreated => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Created },
            AgentEventKind.RunAccepted => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Accepted },
            AgentEventKind.RunRejected => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Rejected },
            AgentEventKind.RunRunning => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Running },
            AgentEventKind.RunCancellationRequested => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.CancellationRequested },
            AgentEventKind.RunCompleted => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Completed },
            AgentEventKind.RunFailed => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Failed },
            AgentEventKind.RunCancelled => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Cancelled },
            AgentEventKind.RunTimedOut => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.TimedOut },
            AgentEventKind.RunDisconnected => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Disconnected },
            AgentEventKind.RunIndeterminate => payload is AgentRunLifecyclePayload { Status: AgentRunStatus.Indeterminate },

            AgentEventKind.UserMessageAdmitted => payload is AgentMessagePayload,
            AgentEventKind.AssistantMessageCompleted => payload is AgentMessagePayload,

            AgentEventKind.FailureReported => payload is AgentFailurePayload,
            AgentEventKind.CapabilitySnapshotChanged => payload is AgentCapabilityChangedPayload,

            _ => false,
        };
}
