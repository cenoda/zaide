using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// No-op implementation for tests and callers that do not wire editor diff tabs.
/// </summary>
public sealed class NullSourceControlDiffTabService : ISourceControlDiffTabService
{
    public static NullSourceControlDiffTabService Instance { get; } = new();

    private NullSourceControlDiffTabService()
    {
    }

    public void OpenOrUpdateDiff(FileChange change)
    {
    }

    public void RefreshOpenDiff(string repositoryRelativePath, FileChange? change)
    {
    }
}
