namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Backend-neutral durable record class. Each class owns separate retention,
/// export, deletion, and migration policy semantics in later milestones.
/// </summary>
internal enum AgentDurableRecordClass
{
    Trace = 0,
    Usage = 1,
    SessionRecovery = 2,
    Audit = 3,
    Memory = 4,
}
