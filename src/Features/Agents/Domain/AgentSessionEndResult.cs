using System;

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
    /// acknowledgement did not complete within the bounded wait. Retryable.
    /// Does not claim the backend stopped.
    /// </summary>
    AcknowledgementIndeterminate,
}

/// <summary>
/// Structured result of <see cref="Contracts.IAgentSessionService.EndAsync"/>.
/// </summary>
internal readonly struct AgentSessionEndResult
{
    public AgentSessionEndResult(
        AgentSessionEndStatus status,
        string? reason = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "End status is invalid.");
        }

        Status = status;
        Reason = reason;
    }

    public AgentSessionEndStatus Status { get; }

    public string? Reason { get; }

    public static AgentSessionEndResult NoLiveSession() =>
        new(AgentSessionEndStatus.NoLiveSession);

    public static AgentSessionEndResult Ended() =>
        new(AgentSessionEndStatus.Ended);

    public static AgentSessionEndResult AcknowledgementIndeterminate(string reason) =>
        new(AgentSessionEndStatus.AcknowledgementIndeterminate, reason);
}
