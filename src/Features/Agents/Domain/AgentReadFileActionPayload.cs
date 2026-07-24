using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable read request for one bounded regular workspace file.
/// </summary>
internal sealed class AgentReadFileActionPayload : AgentActionPayload
{
    public AgentReadFileActionPayload(AgentWorkspaceRelativePath path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public override AgentActionKind Kind => AgentActionKind.ReadFile;

    public AgentWorkspaceRelativePath Path { get; }
}
