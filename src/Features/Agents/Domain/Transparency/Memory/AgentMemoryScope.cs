namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Explicit durable memory scope. Application/global policy is not a memory scope.
/// </summary>
internal enum AgentMemoryScope
{
    Session = 0,
    Agent = 1,
    Conversation = 2,
    ProjectShared = 3,
}
