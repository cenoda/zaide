namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityOperationResult
{
    public AgentSessionContinuityOperationResult(
        AgentSessionContinuityOperationStatus status,
        AgentSessionContinuityOperationKind operation,
        AgentSessionContinuityClassification classification,
        AgentSessionContinuityAcknowledgementState acknowledgementState,
        string? reason = null,
        long? orderingSequence = null)
    {
        Status = status;
        Operation = operation;
        Classification = classification;
        AcknowledgementState = acknowledgementState;
        Reason = reason;
        OrderingSequence = orderingSequence;
    }

    public AgentSessionContinuityOperationStatus Status { get; }

    public AgentSessionContinuityOperationKind Operation { get; }

    public AgentSessionContinuityClassification Classification { get; }

    public AgentSessionContinuityAcknowledgementState AcknowledgementState { get; }

    public string? Reason { get; }

    public long? OrderingSequence { get; }
}
