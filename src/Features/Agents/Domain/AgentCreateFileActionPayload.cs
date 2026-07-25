using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable create request for one workspace file.
/// </summary>
internal sealed class AgentCreateFileActionPayload : AgentActionPayload
{
    public AgentCreateFileActionPayload(AgentWorkspaceRelativePath path, string proposedText)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        ProposedText = ValidateProposedText(proposedText);
        ProposedRevision = AgentContentRevision.FromUtf8Text(ProposedText);
    }

    public override AgentActionKind Kind => AgentActionKind.CreateFile;

    public AgentWorkspaceRelativePath Path { get; }

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
