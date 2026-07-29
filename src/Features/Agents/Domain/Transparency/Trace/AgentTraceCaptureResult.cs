namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Outcome of one <see cref="IAgentTraceCaptureSink.TrySubmit"/> call. The
/// capture pipeline is nonblocking and fail-closed; every admission attempt
/// returns one of these explicit states.
/// </summary>
internal enum AgentTraceCaptureStatus
{
    /// <summary>Redaction succeeded, queue admitted, durable write admitted.</summary>
    Accepted = 0,
    /// <summary>Redaction failed; bounded failure marker was retained.</summary>
    RedactionFailed = 1,
    /// <summary>Payload exceeded the size bound; truncated marker was retained.</summary>
    Truncated = 2,
    /// <summary>Backend did not expose this evidence layer; unavailable marker was retained.</summary>
    Unavailable = 3,
    /// <summary>Capture is disabled for this backend/workspace.</summary>
    Disabled = 4,
    /// <summary>Bounded capture queue was full; submission was rejected (backpressure).</summary>
    BackpressureRejected = 5,
    /// <summary>Submission was invalid (empty payload, missing scope, unknown backend).</summary>
    InvalidRequest = 6,
}

/// <summary>
/// Result of one trace capture attempt with the durable ordering sequence
/// when accepted, or the explicit capture state when rejected.
/// </summary>
internal readonly struct AgentTraceCaptureResult
{
    public AgentTraceCaptureResult(
        AgentTraceCaptureStatus status,
        long orderingSequence = 0,
        AgentTraceCaptureState? captureState = null,
        string? reason = null)
    {
        Status = status;
        OrderingSequence = orderingSequence;
        CaptureState = captureState;
        Reason = reason;
    }

    public AgentTraceCaptureStatus Status { get; }

    public long OrderingSequence { get; }

    public AgentTraceCaptureState? CaptureState { get; }

    public string? Reason { get; }

    public bool IsAdmitted =>
        Status is AgentTraceCaptureStatus.Accepted
            or AgentTraceCaptureStatus.RedactionFailed
            or AgentTraceCaptureStatus.Truncated
            or AgentTraceCaptureStatus.Unavailable;
}
