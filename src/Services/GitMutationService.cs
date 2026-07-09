using LibGit2Sharp;

namespace Zaide.Services;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IGitMutationService"/>.
/// Performs stage/unstage operations against an already-discovered repository
/// root. Does not call <c>Refresh()</c> or update any ViewModel state — it is
/// a pure operation seam, not an orchestration seam.
/// </summary>
public sealed class GitMutationService : IGitMutationService
{
    /// <inheritdoc/>
    public StageResult Stage(string repositoryRoot, string filePath)
    {
        try
        {
            using var repo = new Repository(repositoryRoot);
            Commands.Stage(repo, filePath);
            return StageResult.Success();
        }
        catch (System.Exception ex)
        {
            return StageResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public StageResult Unstage(string repositoryRoot, string filePath)
    {
        try
        {
            using var repo = new Repository(repositoryRoot);
            Commands.Unstage(repo, filePath);
            return StageResult.Success();
        }
        catch (System.Exception ex)
        {
            return StageResult.Failure(ex.Message);
        }
    }
}
