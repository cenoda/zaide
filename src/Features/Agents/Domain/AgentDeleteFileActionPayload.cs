using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable delete request for one workspace file.
/// </summary>
internal sealed class AgentDeleteFileActionPayload : AgentActionPayload
{
    public AgentDeleteFileActionPayload(
        AgentWorkspaceRelativePath path,
        AgentContentRevision baseRevision)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        if (baseRevision == default)
        {
            throw new ArgumentException("Base revision is required.", nameof(baseRevision));
        }

        BaseRevision = baseRevision;
    }

    public override AgentActionKind Kind => AgentActionKind.DeleteFile;

    public AgentWorkspaceRelativePath Path { get; }

    public AgentContentRevision BaseRevision { get; }
}
