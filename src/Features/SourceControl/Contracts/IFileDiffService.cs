using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Application;

namespace Zaide.Features.SourceControl.Contracts;

/// <summary>
/// Narrow diff seam for Source Control. Produces a unified diff for a single
/// <see cref="FileChange"/> against the appropriate git tree:
/// <list type="bullet">
///   <item>Staged files compare HEAD:index (what will be committed).</item>
///   <item>Unstaged files compare HEAD:workdir (working tree changes).</item>
/// </list>
/// Returns null when the file path is not valid in the repository.
/// Returns a result with <see cref="FileDiffResult.IsBinary"/> = true for
/// binary files (no diff text).
/// </summary>
public interface IFileDiffService
{
    /// <summary>
    /// Returns a unified diff for <paramref name="change"/> in the repository
    /// at <paramref name="repositoryRoot"/>, or null when the file path is not
    /// valid in the repository.
    /// </summary>
    FileDiffResult? GetDiff(string repositoryRoot, FileChange change);
}
