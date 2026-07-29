namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal enum AgentUsageCaptureStatus
{
    Accepted = 0,
    Disabled = 1,
    InvalidRequest = 2,
    DuplicateIgnored = 3,
}

internal readonly struct AgentUsageCaptureResult
{
    public AgentUsageCaptureResult(
        AgentUsageCaptureStatus status,
        long orderingSequence = 0,
        string? reason = null)
    {
        Status = status;
        OrderingSequence = orderingSequence;
        Reason = reason;
    }

    public AgentUsageCaptureStatus Status { get; }

    public long OrderingSequence { get; }

    public string? Reason { get; }

    public bool IsAdmitted => Status == AgentUsageCaptureStatus.Accepted;
}
