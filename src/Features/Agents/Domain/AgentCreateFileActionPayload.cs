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

        // Validate budget before fingerprinting
        if (AgentActionBudgets.GetUtf8ByteCount(proposedText) > AgentActionBudgets.ProposedFileTextMaxBytes)
        {
            throw new ArgumentException(
                "Proposed file text exceeds the maximum byte budget.",
                nameof(proposedText));
        }

        // Reject binary content (contains null bytes or control characters)
        if (IsBinaryContent(proposedText))
        {
            throw new ArgumentException(
                "Proposed file text appears to be binary content.",
                nameof(proposedText));
        }

        return proposedText;
    }

    private static bool IsBinaryContent(string text)
    {
        // Check for null bytes which indicate binary content
        if (text.IndexOf('\0') >= 0)
        {
            return true;
        }

        // Check for excessive control characters (more than 10% of content)
        var controlCharCount = 0;
        foreach (var c in text)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                controlCharCount++;
                if (controlCharCount > text.Length / 10)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
