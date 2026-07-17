using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Language.Contracts;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Opens Source Control diffs in the shared editor tab strip. Reuses tabs by
/// repository-relative path and refreshes content from <see cref="IFileDiffService"/>.
/// </summary>
public sealed class SourceControlDiffTabService : ISourceControlDiffTabService
{
    private readonly EditorTabViewModel _editorTabs;
    private readonly IFileDiffService _fileDiffService;
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly IGitRepositoryService _gitRepositoryService;
    private readonly IServiceProvider _services;

    public SourceControlDiffTabService(
        EditorTabViewModel editorTabs,
        IFileDiffService fileDiffService,
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        IGitRepositoryService gitRepositoryService,
        IServiceProvider services)
    {
        _editorTabs = editorTabs;
        _fileDiffService = fileDiffService;
        _workspace = workspace;
        _gitRepositoryService = gitRepositoryService;
        _services = services;
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

        var existing = FindOpenDiffTab(reuseKey);
        if (existing is not null)
        {
            existing.SourceControlComparisonState = comparisonState;
            existing.LoadFileContent(content);
            _editorTabs.ActiveTab = existing;
            _workspace.SetActiveDocument(existing.Document);
            return;
        }

        var document = _workspace.OpenDocument(virtualPath, content);
        document.MarkClean();

        var tab = new EditorViewModel(
            document,
            _services.GetRequiredService<IFileService>(),
            _services.GetService<ISettingsService>(),
            _services.GetService<ILanguageFormattingService>())
        {
            IsReadOnly = true,
            IsSourceControlDiff = true,
            SourceControlDiffKey = reuseKey,
            SourceControlComparisonState = comparisonState,
        };

        _editorTabs.OpenTabs.Add(tab);
        _editorTabs.ActiveTab = tab;
    }

    /// <inheritdoc/>
    public void RefreshOpenDiff(string repositoryRelativePath, FileChange? change)
    {
        if (string.IsNullOrEmpty(repositoryRelativePath))
            return;

        var existing = FindOpenDiffTab(SourceControlDiffTabKey.ToReuseKey(repositoryRelativePath));
        if (existing is null)
            return;

        if (change is null)
        {
            existing.LoadFileContent(SourceControlDiffContent.FormatUnavailable(repositoryRelativePath));
            return;
        }

        var repoRoot = ResolveRepositoryRoot();
        if (repoRoot is null)
            return;

        var diff = _fileDiffService.GetDiff(repoRoot, change);
        existing.SourceControlComparisonState = ToComparisonState(change);
        existing.LoadFileContent(SourceControlDiffContent.Format(change, diff));
    }

    private EditorViewModel? FindOpenDiffTab(string reuseKey) =>
        _editorTabs.OpenTabs.FirstOrDefault(tab =>
            tab.IsSourceControlDiff &&
            string.Equals(tab.SourceControlDiffKey, reuseKey, StringComparison.Ordinal));

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
