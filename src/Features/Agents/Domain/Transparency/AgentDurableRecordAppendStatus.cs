namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Result of attempting to append one durable record.
/// </summary>
internal enum AgentDurableRecordAppendStatus
{
    Appended = 0,
    DuplicateIgnored = 1,
    WorkspaceMismatch = 2,
    WritesDisabled = 3,
    ContentionFailed = 4,
    InvalidRequest = 5,
}
