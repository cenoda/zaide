using Zaide.Features.SourceControl.Domain;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Formats <see cref="FileDiffResult"/> values for display in a read-only editor tab.
/// </summary>
public static class SourceControlDiffContent
{
    public static string Format(FileChange change, FileDiffResult? diff)
    {
        if (diff is null)
        {
            return $"No diff available for {change.FilePath}";
        }

        if (diff.IsBinary)
        {
            return "Binary file — diff not available";
        }

        if (!string.IsNullOrEmpty(diff.DiffText))
        {
            return diff.DiffText;
        }

        return $"No diff available for {change.FilePath}";
    }

    public static string FormatUnavailable(string repositoryRelativePath) =>
        $"No diff available — {repositoryRelativePath} is no longer in the change list.";
}
