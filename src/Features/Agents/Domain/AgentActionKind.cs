namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Closed Phase 17 action taxonomy.
/// </summary>
internal enum AgentActionKind
{
    ReadFile,
    CreateFile,
    ReplaceFile,
    DeleteFile,
    ExecuteCommand,
}
