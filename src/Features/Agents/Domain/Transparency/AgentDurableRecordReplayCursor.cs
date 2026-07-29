namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Replay cursor identifying the last returned ordering sequence per record class.
/// </summary>
internal readonly struct AgentDurableRecordReplayCursor
{
    public AgentDurableRecordReplayCursor(
        AgentDurableRecordClass recordClass,
        long afterOrderingSequence)
    {
        RecordClass = recordClass;
        AfterOrderingSequence = afterOrderingSequence;
    }

    public AgentDurableRecordClass RecordClass { get; }

    public long AfterOrderingSequence { get; }
}
