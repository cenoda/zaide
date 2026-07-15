using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// LibGit2Sharp-backed read-only implementation of <see cref="IGitRepositoryService"/>.
/// Discovers the repository root by walking upward from a starting path and reads
/// branch/HEAD + working-tree status. Read-only for all of Phase 7.1.
/// </summary>
public sealed class GitRepositoryService : IGitRepositoryService
{
    /// <inheritdoc/>
    public RepositoryDiscoveryResult Discover(string startingPath)
    {
        var path = Repository.Discover(startingPath);
        if (string.IsNullOrEmpty(path))
        {
            return RepositoryDiscoveryResult.NotFound(startingPath);
        }

        return RepositoryDiscoveryResult.Found(startingPath, path);
    }

    /// <inheritdoc/>
    public RepositoryStatusSnapshot ReadStatus(string repositoryRoot)
    {
        using var repo = new Repository(repositoryRoot);

        var branches = repo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => new GitBranch(b.FriendlyName, b.IsCurrentRepositoryHead))
            .ToList();

        var isDetached = repo.Info.IsHeadDetached;
        var currentBranchName = isDetached
            ? repo.Head.Tip?.Sha ?? string.Empty
            : (repo.Head.FriendlyName ?? string.Empty);

        var changes = repo.RetrieveStatus()
            .Where(e => e.State != 0)
            .SelectMany(ToChanges)
            .ToList();

        var hasUpstream = false;
        var aheadBy = 0;
        var behindBy = 0;
        if (!isDetached)
        {
            var tracking = repo.Head.TrackingDetails;
            if (tracking is not null && repo.Head.TrackedBranch is not null)
            {
                hasUpstream = true;
                aheadBy = tracking.AheadBy ?? 0;
                behindBy = tracking.BehindBy ?? 0;
            }
        }

        return new RepositoryStatusSnapshot
        {
            CurrentBranchName = currentBranchName,
            IsDetachedHead = isDetached,
            Branches = branches,
            Changes = changes,
            HasUpstream = hasUpstream,
            AheadBy = aheadBy,
            BehindBy = behindBy,
        };
    }

    private static IEnumerable<FileChange> ToChanges(StatusEntry entry)
    {
        var staged = MapChangeType(entry.FilePath, entry.State, staged: true);
        if (staged != null)
        {
            yield return staged;
        }

        var unstaged = MapChangeType(entry.FilePath, entry.State, staged: false);
        if (unstaged != null && unstaged.ChangeType != staged?.ChangeType)
        {
            yield return unstaged;
        }
    }

    private static FileChange? MapChangeType(string filePath, FileStatus state, bool staged)
    {
        if (staged)
        {
            if ((state & FileStatus.NewInIndex) != 0) return new FileChange(filePath, GitChangeType.Added, true);
            if ((state & FileStatus.ModifiedInIndex) != 0) return new FileChange(filePath, GitChangeType.Modified, true);
            if ((state & FileStatus.DeletedFromIndex) != 0) return new FileChange(filePath, GitChangeType.Deleted, true);
            return null;
        }

        if ((state & FileStatus.NewInWorkdir) != 0) return new FileChange(filePath, GitChangeType.Added, false);
        if ((state & FileStatus.ModifiedInWorkdir) != 0) return new FileChange(filePath, GitChangeType.Modified, false);
        if ((state & FileStatus.DeletedFromWorkdir) != 0) return new FileChange(filePath, GitChangeType.Deleted, false);
        return null;
    }
}