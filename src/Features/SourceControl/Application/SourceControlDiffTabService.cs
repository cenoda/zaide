using System;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Opens Source Control diffs in the shared editor tab strip. Reuses tabs by
/// repository-relative path and refreshes content from <see cref="IFileDiffService"/>.
/// Tab mutation is delegated to <see cref="IEditorReadOnlyTabService"/>.
/// </summary>
internal sealed class SourceControlDiffTabService : ISourceControlDiffTabService
{
    private readonly IEditorReadOnlyTabService _readOnlyTabs;
    private readonly IFileDiffService _fileDiffService;
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly IGitRepositoryService _gitRepositoryService;

    public SourceControlDiffTabService(
        IEditorReadOnlyTabService readOnlyTabs,
        IFileDiffService fileDiffService,
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        IGitRepositoryService gitRepositoryService)
    {
        _readOnlyTabs = readOnlyTabs;
        _fileDiffService = fileDiffService;
        _workspace = workspace;
        _gitRepositoryService = gitRepositoryService;
    }

    /// <inheritdoc/>
    public void OpenOrUpdateDiff(FileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (string.IsNullOrEmpty(change.FilePath))
            return;

        var repoRoot = ResolveRepositoryRoot();
        if (repoRoot is null)
            return;

        var diff = _fileDiffService.GetDiff(repoRoot, change);
        var content = SourceControlDiffContent.Format(change, diff);
        var comparisonState = ToComparisonState(change);
        var reuseKey = SourceControlDiffTabKey.ToReuseKey(change.FilePath);
        var virtualPath = SourceControlDiffTabKey.ToVirtualPath(change.FilePath);

        _readOnlyTabs.OpenOrUpdate(new EditorReadOnlyTabRequest(
            reuseKey,
            virtualPath,
            content,
            comparisonState));
    }

    /// <inheritdoc/>
    public void RefreshOpenDiff(string repositoryRelativePath, FileChange? change)
    {
        if (string.IsNullOrEmpty(repositoryRelativePath))
            return;

        var reuseKey = SourceControlDiffTabKey.ToReuseKey(repositoryRelativePath);

        if (change is null)
        {
            _readOnlyTabs.UpdateOpenTab(
                reuseKey,
                SourceControlDiffContent.FormatUnavailable(repositoryRelativePath),
                comparisonStateLabel: null);
            return;
        }

        var repoRoot = ResolveRepositoryRoot();
        if (repoRoot is null)
            return;

        var diff = _fileDiffService.GetDiff(repoRoot, change);
        _readOnlyTabs.UpdateOpenTab(
            reuseKey,
            SourceControlDiffContent.Format(change, diff),
            ToComparisonState(change));
    }

    private string? ResolveRepositoryRoot()
    {
        var workspacePath = _workspace.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath))
            return null;

        var discovery = _gitRepositoryService.Discover(workspacePath);
        return discovery.IsRepository ? discovery.RepositoryRoot : null;
    }

    private static string ToComparisonState(FileChange change) =>
        change.IsStaged ? "Staged Changes" : "Changes";
}
