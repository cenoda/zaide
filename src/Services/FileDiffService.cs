using System;
using System.Linq;
using LibGit2Sharp;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IFileDiffService"/>.
/// Produces unified diffs for staged (HEAD:index) and unstaged (HEAD:workdir)
/// file changes. Uses <c>TreeChanges</c> for path existence, then <c>Patch</c>
/// for unified diff text and binary detection.
/// </summary>
public sealed class FileDiffService : IFileDiffService
{
    /// <inheritdoc/>
    public FileDiffResult? GetDiff(string repositoryRoot, FileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (string.IsNullOrEmpty(change.FilePath))
        {
            return null;
        }

        using var repo = new Repository(repositoryRoot);

        var diffTargets = change.IsStaged
            ? DiffTargets.Index
            : DiffTargets.WorkingDirectory;

        var filePaths = new[] { change.FilePath };

        // Step 1: Check path existence via TreeChanges.
        var treeChanges = repo.Diff.Compare<TreeChanges>(
            repo.Head.Tip.Tree,
            diffTargets,
            filePaths);

        bool pathExists = treeChanges.Any(e =>
            string.Equals(e.Path, change.FilePath, StringComparison.Ordinal));

        if (!pathExists)
        {
            return null;
        }

        // Step 2: Produce the unified diff text via Patch.
        var patch = repo.Diff.Compare<Patch>(
            repo.Head.Tip.Tree,
            diffTargets,
            filePaths);

        var entry = patch[change.FilePath];
        if (entry == null)
        {
            return null;
        }

        if (entry.IsBinaryComparison)
        {
            return new FileDiffResult
            {
                FilePath = change.FilePath,
                IsBinary = true,
            };
        }

        return new FileDiffResult
        {
            FilePath = change.FilePath,
            DiffText = patch.Content,
            AddedLines = entry.LinesAdded,
            DeletedLines = entry.LinesDeleted,
        };
    }
}
