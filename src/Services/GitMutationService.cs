using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using LibGit2Sharp;

namespace Zaide.Services;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IGitMutationService"/>.
/// Performs stage/unstage/commit operations against an already-discovered
/// repository root. Push delegates to the system <c>git</c> CLI so SSH,
/// HTTPS credential helpers, and other user-configured remotes work. Does not
/// call <c>Refresh()</c> or update any ViewModel state — it is a pure
/// operation seam, not an orchestration seam.
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

    /// <inheritdoc/>
    public PushResult Push(string repositoryRoot)
    {
        try
        {
            using var repo = new Repository(repositoryRoot);

            if (repo.Info.IsHeadDetached)
                return PushResult.Failure("Cannot push from detached HEAD.");

            if (repo.RetrieveStatus().IsDirty)
                return PushResult.Failure("Cannot push with uncommitted changes.");

            var branch = repo.Head;
            if (branch.TrackedBranch is null)
                return PushResult.Failure("Current branch has no upstream branch.");

            var tracking = branch.TrackingDetails;
            if (tracking is null || (tracking.AheadBy ?? 0) == 0)
                return PushResult.Failure("Nothing to push.");

            return PushViaGitCli(repositoryRoot);
        }
        catch (System.Exception ex)
        {
            return PushResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Runs <c>git push</c> in <paramref name="repositoryRoot"/> so remotes
    /// using SSH or host credential helpers work outside LibGit2Sharp limits.
    /// </summary>
    private static PushResult PushViaGitCli(string repositoryRoot)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "push",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return PushResult.Failure("Failed to start git push.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return PushResult.Success();

            var message = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim()
                : !string.IsNullOrWhiteSpace(stdout) ? stdout.Trim()
                : $"git push failed with exit code {process.ExitCode}.";
            return PushResult.Failure(message);
        }
        catch (Win32Exception)
        {
            return PushResult.Failure("git was not found on PATH. Install git to push.");
        }
        catch (System.Exception ex)
        {
            return PushResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public CommitResult Commit(string repositoryRoot, string message)
    {
        // Empty-message validation happens before any repository access so
        // callers (and tests) can verify no git call is made for this case.
        if (string.IsNullOrWhiteSpace(message))
            return CommitResult.Failure("Commit message cannot be empty.");

        try
        {
            using var repo = new Repository(repositoryRoot);

            var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
            if (signature is null)
            {
                return CommitResult.Failure(
                    "Git user identity is not configured. Set user.name and user.email in your git config.");
            }

            var status = repo.RetrieveStatus();
            if (!HasStagedChanges(status))
                return CommitResult.Failure("Nothing staged to commit.");

            var commit = repo.Commit(message, signature, signature);
            return CommitResult.Success(commit.Sha);
        }
        catch (System.Exception ex)
        {
            return CommitResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Returns true when the repository status contains at least one entry
    /// whose state includes an index (staged) flag.
    /// </summary>
    private static bool HasStagedChanges(RepositoryStatus status)
    {
        foreach (var entry in status)
        {
            var state = entry.State;
            if ((state & FileStatus.NewInIndex) != 0
                || (state & FileStatus.ModifiedInIndex) != 0
                || (state & FileStatus.DeletedFromIndex) != 0
                || (state & FileStatus.RenamedInIndex) != 0)
            {
                return true;
            }
        }
        return false;
    }
}
