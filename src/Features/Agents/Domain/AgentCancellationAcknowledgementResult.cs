using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Outcome of a backend cancellation-acknowledgement attempt during explicit end
/// or a subsequent retry.
/// </summary>
internal enum AgentCancellationAcknowledgementStatus
{
    /// <summary>Backend confirmed the cancellation request was acknowledged.</summary>
    Succeeded,

    /// <summary>Bounded cancel-ack wait elapsed without confirmation.</summary>
    TimedOut,

    /// <summary>Cancel-ack request failed without confirmation.</summary>
    Failed,

    /// <summary>
    /// No pending cancel-ack target exists for the session (or the backend cannot
    /// reissue). Does not imply provider termination.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Structured result of
/// <see cref="Contracts.IAgentCancellationAcknowledgementBackend.AcknowledgeCancellationAsync"/>.
/// </summary>
internal readonly struct AgentCancellationAcknowledgementResult
{
    public AgentCancellationAcknowledgementResult(
        AgentCancellationAcknowledgementStatus status,
        string? reason = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Status is invalid.");
        }

        Status = status;
        Reason = reason;
    }

    public AgentCancellationAcknowledgementStatus Status { get; }

    public string? Reason { get; }

    public static AgentCancellationAcknowledgementResult Succeeded() =>
        new(AgentCancellationAcknowledgementStatus.Succeeded);

    public static AgentCancellationAcknowledgementResult TimedOut(string reason) =>
        new(AgentCancellationAcknowledgementStatus.TimedOut, reason);

    public static AgentCancellationAcknowledgementResult Failed(string reason) =>
        new(AgentCancellationAcknowledgementStatus.Failed, reason);

    public static AgentCancellationAcknowledgementResult Unavailable(string reason) =>
        new(AgentCancellationAcknowledgementStatus.Unavailable, reason);
}
