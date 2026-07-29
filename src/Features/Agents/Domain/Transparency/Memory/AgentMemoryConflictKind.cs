namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Conflict, poisoning, and staleness markers evaluated at write time.
/// </summary>
internal enum AgentMemoryConflictKind
{
    None = 0,
    ScopeConflict = 1,
    ContentConflict = 2,
    PoisoningSuspect = 3,
    StaleFact = 4,
    Superseded = 5,
}
