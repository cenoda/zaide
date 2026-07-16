using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Opens Source Control file diffs in the main editor tab strip with reuse and refresh.
/// </summary>
public interface ISourceControlDiffTabService
{
    /// <summary>
    /// Opens or focuses a read-only diff tab for <paramref name="change"/>.
    /// Reuses an existing tab when the repository-relative path already has one open.
    /// </summary>
    void OpenOrUpdateDiff(FileChange change);

    /// <summary>
    /// Refreshes an open diff tab after repository state changes. When
    /// <paramref name="change"/> is null the tab shows an unavailable notice.
    /// </summary>
    void RefreshOpenDiff(string repositoryRelativePath, FileChange? change);
}
