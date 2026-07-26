namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One redacted, token-estimated context candidate before budget enforcement.
/// </summary>
internal sealed class AgentContextManifestCandidate
{
    public AgentContextManifestCandidate(AgentContextItem item, int priority)
    {
        Item = item;
        Priority = priority;
    }

    public AgentContextItem Item { get; }

    public int Priority { get; }
}
