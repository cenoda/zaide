namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Stable identity for Source Control diff editor tabs. Diff tabs use a virtual
/// document path so they never collide with regular file tabs for the same repo path.
/// </summary>
public static class SourceControlDiffTabKey
{
    public const string VirtualPathPrefix = "zaide-sc-diff://";

    /// <summary>
    /// Repository-relative path used to find an existing diff tab for reuse.
    /// </summary>
    public static string ToReuseKey(string repositoryRelativePath) =>
        repositoryRelativePath;

    /// <summary>
    /// Virtual workspace document path for a diff tab.
    /// </summary>
    public static string ToVirtualPath(string repositoryRelativePath) =>
        $"{VirtualPathPrefix}{repositoryRelativePath}";
}
