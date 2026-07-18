using System;
using System.Linq;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Presentation-owned gateway that opens and updates read-only editor tabs.
/// </summary>
internal sealed class EditorReadOnlyTabService : IEditorReadOnlyTabService
{
    private readonly EditorTabViewModel _editorTabs;
    private readonly IEditorSessionFactory _sessionFactory;
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;

    public EditorReadOnlyTabService(
        EditorTabViewModel editorTabs,
        IEditorSessionFactory sessionFactory,
        global::Zaide.Features.Workspace.Domain.Workspace workspace)
    {
        _editorTabs = editorTabs;
        _sessionFactory = sessionFactory;
        _workspace = workspace;
    }

    /// <inheritdoc/>
    public void OpenOrUpdate(EditorReadOnlyTabRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = FindOpenTab(request.ReuseKey);
        if (existing is not null)
        {
            existing.SourceControlComparisonState = request.ComparisonStateLabel;
            existing.LoadFileContent(request.Content);
            _editorTabs.ActiveTab = existing;
            _workspace.SetActiveDocument(existing.Document);
            return;
        }

        var document = _workspace.OpenDocument(request.VirtualPath, request.Content);
        document.MarkClean();

        var tab = _sessionFactory.Create(document);
        tab.IsReadOnly = true;
        tab.IsSourceControlDiff = true;
        tab.SourceControlDiffKey = request.ReuseKey;
        tab.SourceControlComparisonState = request.ComparisonStateLabel;

        _editorTabs.OpenTabs.Add(tab);
        _editorTabs.ActiveTab = tab;
    }

    /// <inheritdoc/>
    public void UpdateOpenTab(string reuseKey, string content, string? comparisonStateLabel)
    {
        var existing = FindOpenTab(reuseKey);
        if (existing is null)
            return;

        if (comparisonStateLabel is not null)
            existing.SourceControlComparisonState = comparisonStateLabel;

        existing.LoadFileContent(content);
    }

    private EditorViewModel? FindOpenTab(string reuseKey) =>
        _editorTabs.OpenTabs.FirstOrDefault(tab =>
            tab.IsSourceControlDiff &&
            string.Equals(tab.SourceControlDiffKey, reuseKey, StringComparison.Ordinal));
}
