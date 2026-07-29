namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal enum AgentMemoryOperationStatus
{
    Accepted = 0,
    DuplicateIgnored = 1,
    Rejected = 2,
    WorkspaceDenied = 3,
    NotFound = 4,
    InvalidRequest = 5,
    ConflictDetected = 6,
}
