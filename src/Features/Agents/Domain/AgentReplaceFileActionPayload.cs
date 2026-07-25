using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable replace request for one workspace file.
/// </summary>
internal sealed class AgentReplaceFileActionPayload : AgentActionPayload
{
    public AgentReplaceFileActionPayload(
        AgentWorkspaceRelativePath path,
        AgentContentRevision baseRevision,
        string proposedText)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        if (baseRevision == default)
        {
            throw new ArgumentException("Base revision is required.", nameof(baseRevision));
        }

        BaseRevision = baseRevision;
        ProposedText = ValidateProposedText(proposedText);
        ProposedRevision = AgentContentRevision.FromUtf8Text(ProposedText);
    }

    public override AgentActionKind Kind => AgentActionKind.ReplaceFile;

    public AgentWorkspaceRelativePath Path { get; }

    public AgentContentRevision BaseRevision { get; }

    public string ProposedText { get; }

    public AgentContentRevision ProposedRevision { get; }

    private static string ValidateProposedText(string proposedText)
    {
        ArgumentNullException.ThrowIfNull(proposedText);
        if (AgentActionBudgets.GetUtf8ByteCount(proposedText) > AgentActionBudgets.ProposedFileTextMaxBytes)
        {
            throw new ArgumentException(
                "Proposed file text exceeds the maximum byte budget.",
                nameof(proposedText));
        }

        return proposedText;
    }
}
