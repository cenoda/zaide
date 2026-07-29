namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Append attempt outcome with the admitted envelope when successful.
/// </summary>
internal readonly struct AgentDurableRecordAppendResult
{
    public AgentDurableRecordAppendResult(
        AgentDurableRecordAppendStatus status,
        AgentDurableRecordEnvelope? envelope = null)
    {
        Status = status;
        Envelope = envelope;
    }

    public AgentDurableRecordAppendStatus Status { get; }

    public AgentDurableRecordEnvelope? Envelope { get; }

    public bool IsSuccess =>
        Status is AgentDurableRecordAppendStatus.Appended
            or AgentDurableRecordAppendStatus.DuplicateIgnored;
}
