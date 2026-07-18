namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Opens and updates read-only editor tabs (for example Source Control diffs)
/// without exposing editor presentation types to application callers.
/// </summary>
public interface IEditorReadOnlyTabService
{
    /// <summary>
    /// Opens a new read-only tab or updates an existing tab matched by
    /// <see cref="EditorReadOnlyTabRequest.ReuseKey"/>.
    /// </summary>
    void OpenOrUpdate(EditorReadOnlyTabRequest request);

    /// <summary>
    /// Updates content (and optionally the comparison state label) for an open
    /// tab with the given reuse key. No-op when no matching tab is open.
    /// </summary>
    void UpdateOpenTab(string reuseKey, string content, string? comparisonStateLabel);
}

/// <summary>
/// Request to open or refresh a read-only editor tab.
/// </summary>
/// <param name="ReuseKey">Stable key used to find and reuse an existing tab.</param>
/// <param name="VirtualPath">Virtual document path used when creating a new tab.</param>
/// <param name="Content">Tab text content.</param>
/// <param name="ComparisonStateLabel">Comparison state label shown on the tab.</param>
public sealed record EditorReadOnlyTabRequest(
    string ReuseKey,
    string VirtualPath,
    string Content,
    string ComparisonStateLabel);
