using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Editor.Application;

/// <summary>
/// Reconciles confirmed workspace file mutations with open editor documents
/// through the Workspace/Editor application boundary.
/// </summary>
internal sealed class WorkspaceEditorDocumentReconciler : IAgentDocumentReconciler
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly IEditorUiDispatcher _uiDispatcher;
    private readonly ILogger<WorkspaceEditorDocumentReconciler> _logger;

    public WorkspaceEditorDocumentReconciler(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        IEditorUiDispatcher uiDispatcher,
        ILogger<WorkspaceEditorDocumentReconciler> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AgentDocumentReconciliationResult ReconcileAfterMutation(
        WorkspaceActionScope scope,
        IWorkspaceActionAuthority workspaceAuthority,
        AgentFileActionProposal proposal,
        AgentFileMutationResult mutationResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(workspaceAuthority);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(mutationResult);

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.NotApplicable,
                "Document reconciliation was cancelled.");
        }

        if (!mutationResult.IsSuccess)
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.NotApplicable,
                "Disk mutation did not succeed; editor reconciliation skipped.");
        }

        if (!workspaceAuthority.IsCurrent(scope))
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.StaleWorkspace,
                "Workspace generation changed before document reconciliation could apply.");
        }

        var absolutePath = ResolveAbsolutePath(scope, proposal);
        var openDocument = TryFindOpenDocument(absolutePath);
        if (openDocument is null)
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.NotApplicable,
                "No open document required reconciliation.");
        }

        try
        {
            return _uiDispatcher.Invoke(() =>
                ReconcileOpenDocument(
                    openDocument,
                    absolutePath,
                    proposal.Operation,
                    mutationResult));
        }
        catch (Exception ex)
        {
            _logger.Log(
                LogLevel.Debug,
                new EventId(17060, nameof(ReconcileAfterMutation)),
                ex,
                "Document reconciliation failed for {Path}",
                absolutePath);
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.NotApplicable,
                "Document reconciliation failed without mutating the editor buffer.");
        }
    }

    private AgentDocumentReconciliationResult ReconcileOpenDocument(
        Document document,
        string absolutePath,
        AgentFileProposalOperation operation,
        AgentFileMutationResult mutationResult)
    {
        if (operation == AgentFileProposalOperation.Delete)
        {
            if (File.Exists(absolutePath))
            {
                return AgentDocumentReconciliationResult.Create(
                    AgentDocumentReconciliationOutcome.PostMutationRace,
                    "Disk file reappeared before document reconciliation could confirm deletion.");
            }

            if (document.IsDirty)
            {
                document.FlagDiskAbsent();
                return AgentDocumentReconciliationResult.Create(
                    AgentDocumentReconciliationOutcome.DiskDeletedDirty,
                    "Dirty open document preserved; disk file is absent.");
            }

            document.FlagDiskAbsent();
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.DiskDeletedClean,
                "Clean open document retained; disk file is absent.");
        }

        if (!File.Exists(absolutePath))
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.PostMutationRace,
                "Disk file disappeared before document reconciliation could reload content.");
        }

        string diskContent;
        try
        {
            diskContent = File.ReadAllText(absolutePath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Log(
                LogLevel.Debug,
                new EventId(17061, nameof(ReconcileOpenDocument)),
                ex,
                "Unable to read disk content for reconciliation at {Path}",
                absolutePath);
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.NotApplicable,
                "Document reconciliation could not read confirmed disk content.");
        }

        var currentRevision = AgentContentRevision.FromUtf8Text(diskContent);
        if (!currentRevision.Equals(mutationResult.Revision))
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.PostMutationRace,
                "Disk content changed again before document reconciliation could apply.");
        }

        if (document.IsDirty)
        {
            return AgentDocumentReconciliationResult.Create(
                AgentDocumentReconciliationOutcome.ExternalConflict,
                "Dirty open document preserved; disk content changed externally.");
        }

        document.ReloadCleanContent(diskContent);
        return AgentDocumentReconciliationResult.Create(
            AgentDocumentReconciliationOutcome.ReloadedClean,
            "Clean open document reloaded from confirmed disk content.");
    }

    private Document? TryFindOpenDocument(string absolutePath)
    {
        foreach (var document in _workspace.Documents)
        {
            if (string.Equals(
                    NormalizePath(document.FilePath),
                    absolutePath,
                    StringComparison.Ordinal))
            {
                return document;
            }
        }

        return null;
    }

    private static string ResolveAbsolutePath(
        WorkspaceActionScope scope,
        AgentFileActionProposal proposal) =>
        NormalizePath(Path.Combine(scope.CapturedCanonicalRoot, proposal.Path.NormalizedPath));

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
}
