using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Outcome of an explicit live-session end attempt.
/// </summary>
internal enum AgentSessionEndStatus
{
    /// <summary>No live session ownership existed for the conversation.</summary>
    NoLiveSession,

    /// <summary>
    /// Local terminalization completed: run cancelled or already terminal,
    /// <c>SessionEnded</c> emitted, and live ownership removed.
    /// Does not claim provider-side process deletion or remote termination.
    /// </summary>
    Ended,

    /// <summary>
    /// Cancellation was requested and local ownership remains, but backend
    /// acknowledgement did not complete within the bounded wait, or cancel
    /// acknowledgement was uncertain. Retryable. Does not claim the backend stopped.
    /// </summary>
    AcknowledgementIndeterminate,
}

/// <summary>
/// Structured result of <see cref="Contracts.IAgentSessionService.EndAsync"/>.
/// Carries session/run/attempt correlation for projection dedupe; raw correlation
/// identifiers must not be shown in user-facing text.
/// </summary>
internal readonly struct AgentSessionEndResult
{
    public AgentSessionEndResult(
        AgentSessionEndStatus status,
        string? reason = null,
        AgentSessionId? sessionId = null,
        ExecutionRunId? runId = null,
        ConversationEntryCorrelationId? attemptCorrelation = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "End status is invalid.");
        }

        if (status == AgentSessionEndStatus.AcknowledgementIndeterminate
            && attemptCorrelation is null)
        {
            throw new ArgumentException(
                "Indeterminate termination requires attempt correlation.",
                nameof(attemptCorrelation));
        }

        Status = status;
        Reason = reason;
        SessionId = sessionId;
        RunId = runId;
        AttemptCorrelation = attemptCorrelation;
    }

    public AgentSessionEndStatus Status { get; }

    public string? Reason { get; }

    /// <summary>Live session id for this end attempt, when known.</summary>
    public AgentSessionId? SessionId { get; }

    /// <summary>Active or ending run id for this end attempt, when known.</summary>
    public ExecutionRunId? RunId { get; }

    /// <summary>
    /// Opaque termination-attempt correlation for exactly-once projection.
    /// Not for user-facing display.
    /// </summary>
    public ConversationEntryCorrelationId? AttemptCorrelation { get; }

    public static AgentSessionEndResult NoLiveSession() =>
        new(AgentSessionEndStatus.NoLiveSession);

    public static AgentSessionEndResult Ended(
        AgentSessionId? sessionId = null,
        ExecutionRunId? runId = null) =>
        new(AgentSessionEndStatus.Ended, reason: null, sessionId, runId);

    public static AgentSessionEndResult AcknowledgementIndeterminate(
        string reason,
        AgentSessionId sessionId,
        ExecutionRunId? runId,
        ConversationEntryCorrelationId attemptCorrelation) =>
        new(
            AgentSessionEndStatus.AcknowledgementIndeterminate,
            reason,
            sessionId,
            runId,
            attemptCorrelation);
}
