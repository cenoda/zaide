using System;
using System.Threading;
using System.Text;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Generates immutable file action proposals from action payloads.
/// This is the M4 non-mutating proposal creation boundary.
/// </summary>
internal static class AgentFileProposalGenerator
{
    private const int MaxPreviewLines = 50;
    private const int MaxPreviewBytes = 8 * 1024; // 8 KB preview budget
    private const int DiffContextLines = 3;

    /// <summary>
    /// Creates an immutable file action proposal from a file action payload.
    /// For replace and delete operations, this reads the current base file state.
    /// For create operations, this validates the proposed content.
    /// </summary>
    /// <param name="workspaceScope">The workspace scope for the proposal.</param>
    /// <param name="payload">The file action payload.</param>
    /// <param name="fileReader">The file reader for accessing base file state.</param>
    /// <param name="permissionFingerprint">The permission fingerprint for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid proposal, or an error result if the proposal cannot be created.</returns>
    public static AgentFileProposalResult CreateProposal(
        WorkspaceActionScope workspaceScope,
        AgentActionPayload payload,
        IAgentFileReader fileReader,
        AgentActionRequestFingerprint permissionFingerprint,
        CancellationToken cancellationToken)
    {
        if (workspaceScope is null) throw new ArgumentNullException(nameof(workspaceScope));
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (fileReader is null) throw new ArgumentNullException(nameof(fileReader));
        if (permissionFingerprint == default) throw new ArgumentException("Permission fingerprint is required.", nameof(permissionFingerprint));

        try
        {
            var proposal = payload switch
            {
                AgentCreateFileActionPayload create => CreateCreateProposal(workspaceScope, create, permissionFingerprint, fileReader, cancellationToken),
                AgentReplaceFileActionPayload replace => CreateReplaceProposal(workspaceScope, replace, fileReader, permissionFingerprint, cancellationToken),
                AgentDeleteFileActionPayload delete => CreateDeleteProposal(workspaceScope, delete, fileReader, permissionFingerprint, cancellationToken),
                _ => throw new ArgumentException("Unsupported file action payload type.", nameof(payload))
            };

            return AgentFileProposalResult.Success(proposal);
        }
        catch (Exception exception)
        {
            return AgentFileProposalResult.Failed(exception);
        }
    }

    private static AgentFileActionProposal CreateCreateProposal(
        WorkspaceActionScope workspaceScope,
        AgentCreateFileActionPayload create,
        AgentActionRequestFingerprint permissionFingerprint,
        IAgentFileReader fileReader,
        CancellationToken cancellationToken)
    {
        // Validate proposed content budget
        ValidateProposedContentBudget(create.ProposedText);

        // Create proposals succeed only when the target is definitively absent.
        var readResult = fileReader.Read(workspaceScope, create.Path, cancellationToken);
        ValidateCreateTargetAbsent(readResult, create.Path);

        // For create operations, base does not exist
        var baseExists = false;
        var baseRevision = (AgentContentRevision?)null;
        var permissionFingerprintBaseRevision = (AgentContentRevision?)null;

        // Create bounded change summary
        var changeSummary = BuildCreateChangeSummary(create);

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            create.Path,
            baseExists,
            baseRevision,
            create.ProposedRevision,
            changeSummary);

        var proposalId = AgentFileProposalId.New();
        return new AgentFileActionProposal(
            proposalId,
            proposal,
            workspaceScope,
            permissionFingerprint,
            permissionFingerprintBaseRevision);
    }

    private static AgentFileActionProposal CreateReplaceProposal(
        WorkspaceActionScope workspaceScope,
        AgentReplaceFileActionPayload replace,
        IAgentFileReader fileReader,
        AgentActionRequestFingerprint permissionFingerprint,
        CancellationToken cancellationToken)
    {
        // Validate proposed content budget
        ValidateProposedContentBudget(replace.ProposedText);

        // Read current base file state
        var readResult = fileReader.Read(workspaceScope, replace.Path, cancellationToken);

        if (readResult.Outcome != AgentFileReadOutcome.Succeeded)
        {
            throw new InvalidOperationException(
                $"Base file read failed for replace proposal: {readResult.Summary}");
        }

        // Validate that the current base revision matches the payload's base revision
        if (!replace.BaseRevision.Equals(readResult.Revision))
        {
            throw new InvalidOperationException(
                "Base file content has changed since the action was requested. " +
                $"Expected: {replace.BaseRevision.Value}, Actual: {readResult.Revision.Value}");
        }

        // For replace operations, base exists and we have the current revision
        var baseExists = true;
        var baseRevision = readResult.Revision;
        var permissionFingerprintBaseRevision = replace.BaseRevision; // From the original request

        // Create bounded change summary with diff
        var changeSummary = BuildReplaceChangeSummary(replace, readResult.Content);

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            replace.Path,
            baseExists,
            baseRevision,
            replace.ProposedRevision,
            changeSummary);

        var proposalId = AgentFileProposalId.New();
        return new AgentFileActionProposal(
            proposalId,
            proposal,
            workspaceScope,
            permissionFingerprint,
            permissionFingerprintBaseRevision);
    }

    private static AgentFileActionProposal CreateDeleteProposal(
        WorkspaceActionScope workspaceScope,
        AgentDeleteFileActionPayload delete,
        IAgentFileReader fileReader,
        AgentActionRequestFingerprint permissionFingerprint,
        CancellationToken cancellationToken)
    {
        // Read current base file state for delete
        var readResult = fileReader.Read(workspaceScope, delete.Path, cancellationToken);

        if (readResult.Outcome != AgentFileReadOutcome.Succeeded)
        {
            throw new InvalidOperationException(
                $"Base file read failed for delete proposal: {readResult.Summary}");
        }

        // Validate that the current base revision matches the payload's base revision
        if (!delete.BaseRevision.Equals(readResult.Revision))
        {
            throw new InvalidOperationException(
                "Base file content has changed since the action was requested. " +
                $"Expected: {delete.BaseRevision.Value}, Actual: {readResult.Revision.Value}");
        }

        // For delete operations, base exists and we have the current revision
        var baseExists = true;
        var baseRevision = readResult.Revision;
        var permissionFingerprintBaseRevision = delete.BaseRevision; // From the original request

        // Create bounded change summary
        var changeSummary = BuildDeleteChangeSummary(delete, readResult.Content);

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Delete,
            delete.Path,
            baseExists,
            baseRevision,
            null, // No proposed revision for delete
            changeSummary);

        var proposalId = AgentFileProposalId.New();
        return new AgentFileActionProposal(
            proposalId,
            proposal,
            workspaceScope,
            permissionFingerprint,
            permissionFingerprintBaseRevision);
    }

    private static void ValidateCreateTargetAbsent(
        AgentFileReadResult readResult,
        AgentWorkspaceRelativePath path)
    {
        if (readResult.Outcome == AgentFileReadOutcome.NotFound)
        {
            return;
        }

        var message = readResult.Outcome switch
        {
            AgentFileReadOutcome.Succeeded =>
                "Create proposal rejected: file already exists at " + path.NormalizedPath,
            AgentFileReadOutcome.Cancelled =>
                "Create proposal rejected: target inspection was cancelled.",
            _ =>
                $"Create proposal rejected: target state is indeterminate ({readResult.Outcome}).",
        };

        throw new InvalidOperationException(message);
    }

    private static void ValidateProposedContentBudget(string proposedText)
    {
        if (AgentActionBudgets.GetUtf8ByteCount(proposedText) > AgentActionBudgets.ProposedFileTextMaxBytes)
        {
            throw new ArgumentException(
                "Proposed file text exceeds the maximum byte budget.",
                nameof(proposedText));
        }
    }

    private static string BuildCreateChangeSummary(AgentCreateFileActionPayload create)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Create file: {create.Path.NormalizedPath}");
        builder.AppendLine($"Proposed revision: {create.ProposedRevision.Value}");
        builder.AppendLine();
        builder.AppendLine("Operation: create");
        builder.AppendLine("Affected paths: " + create.Path.NormalizedPath);
        builder.AppendLine();
        builder.AppendLine("Preview:");
        builder.AppendLine(TruncatePreview(create.ProposedText));
        return builder.ToString();
    }

    private static string BuildReplaceChangeSummary(AgentReplaceFileActionPayload replace, string? baseContent)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Replace file: {replace.Path.NormalizedPath}");
        builder.AppendLine($"Base revision: {replace.BaseRevision.Value}");
        builder.AppendLine($"Proposed revision: {replace.ProposedRevision.Value}");
        builder.AppendLine();
        builder.AppendLine("Operation: replace");
        builder.AppendLine("Affected paths: " + replace.Path.NormalizedPath);
        builder.AppendLine();
        builder.AppendLine("Preview:");
        builder.AppendLine(TruncatePreview(replace.ProposedText));
        return builder.ToString();
    }

    private static string BuildDeleteChangeSummary(AgentDeleteFileActionPayload delete, string? baseContent)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Delete file: {delete.Path.NormalizedPath}");
        builder.AppendLine($"Base revision: {delete.BaseRevision.Value}");
        builder.AppendLine();
        builder.AppendLine("Operation: delete");
        builder.AppendLine("Affected paths: " + delete.Path.NormalizedPath);
        builder.AppendLine();
        builder.AppendLine("Current content preview:");
        builder.AppendLine(TruncatePreview(baseContent));
        return builder.ToString();
    }

    internal static string TruncatePreview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(empty)";
        }

        var lines = text.Split('\n');
        var lineCount = 0;
        var byteCount = 0;
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (lineCount >= MaxPreviewLines)
            {
                builder.AppendLine("... (truncated)");
                break;
            }

            var lineByteCount = Encoding.UTF8.GetByteCount(line);
            if (byteCount + lineByteCount > MaxPreviewBytes)
            {
                // Add as much of this line as we can
                var availableBytes = MaxPreviewBytes - byteCount;
                if (availableBytes > 0)
                {
                    var charsToTake = Encoding.UTF8.GetMaxCharCount(availableBytes);
                    if (charsToTake > 0)
                    {
                        builder.Append(line.AsSpan(0, Math.Min(charsToTake, line.Length)));
                    }
                }
                builder.AppendLine("... (truncated)");
                break;
            }

            builder.AppendLine(line);
            byteCount += lineByteCount + 1; // +1 for newline
            lineCount++;
        }

        return builder.ToString().TrimEnd();
    }
}