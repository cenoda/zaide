using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Xunit;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;

namespace Zaide.Tests.Features.SourceControl.Infrastructure;

/// <summary>
/// Repo-backed tests for <see cref="GitMutationService"/>. Each test creates a
/// temporary git repository, sets up the appropriate file state, calls the
/// service, and verifies the resulting index state via <see cref="Repository.RetrieveStatus()"/>.
/// </summary>
public class GitMutationServiceTests
{
    private static string CreateTempDir()
    {
        return Directory.CreateTempSubdirectory("zaide-mutation-test-").FullName;
    }

    private static string InitRepoWithCommit(
        string path,
        string fileName = "file.txt",
        string content = "hello")
    {
        Repository.Init(path);
        using var repo = new Repository(path);
        var filePath = Path.Combine(path, fileName);
        File.WriteAllText(filePath, content);
        Commands.Stage(repo, filePath);
        var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
        repo.Commit("initial", author, author);
        return path;
    }

    private readonly IGitMutationService _service = new GitMutationService();

    private static void ConfigureUpstreamAfterInitialPush(string localDir)
    {
        using var local = new Repository(localDir);
        local.Branches.Update(local.Head, b =>
            b.TrackedBranch = $"refs/remotes/origin/{local.Head.FriendlyName}");
    }

    [Fact]
    public void Stage_UntrackedFile_MovesToIndex()
    {
        var dir = CreateTempDir();
        try
        {
            Repository.Init(dir);
            File.WriteAllText(Path.Combine(dir, "new.txt"), "content");

            var result = _service.Stage(dir, "new.txt");

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);

            using var repo = new Repository(dir);
            var status = repo.RetrieveStatus("new.txt");
            Assert.Equal(FileStatus.NewInIndex, status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Stage_ModifiedFile_MovesToIndex()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\n");
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nline2\n");

            var result = _service.Stage(dir, "modify.txt");

            Assert.True(result.IsSuccess);

            using var repo = new Repository(dir);
            var status = repo.RetrieveStatus("modify.txt");
            Assert.Equal(FileStatus.ModifiedInIndex, status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unstage_StagedFile_RemovesFromIndex()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\n");
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nline2\n");
            using (var repo = new Repository(dir))
            {
                Commands.Stage(repo, "modify.txt");
            }

            var result = _service.Unstage(dir, "modify.txt");

            Assert.True(result.IsSuccess);

            using var repoAfter = new Repository(dir);
            var status = repoAfter.RetrieveStatus("modify.txt");
            Assert.Equal(FileStatus.ModifiedInWorkdir, status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Stage_AlreadyStagedFile_IsNoOpSuccess()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\n");
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nline2\n");
            using (var repo = new Repository(dir))
            {
                Commands.Stage(repo, "modify.txt");
            }

            var result = _service.Stage(dir, "modify.txt");

            Assert.True(result.IsSuccess);

            using var repoAfter = new Repository(dir);
            var status = repoAfter.RetrieveStatus("modify.txt");
            Assert.Equal(FileStatus.ModifiedInIndex, status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unstage_AlreadyUnstagedFile_IsNoOpSuccess()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\n");
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nline2\n");

            var result = _service.Unstage(dir, "modify.txt");

            Assert.True(result.IsSuccess);

            using var repoAfter = new Repository(dir);
            var status = repoAfter.RetrieveStatus("modify.txt");
            Assert.Equal(FileStatus.ModifiedInWorkdir, status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Stage_NonExistentRepository_ReturnsFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zaide-mutation-nonexistent-" + Guid.NewGuid());

        var result = _service.Stage(dir, "file.txt");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void StageAll_MultipleUntrackedAndModifiedFiles_MovesAllToIndex()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "tracked.txt", "v1\n");
            File.WriteAllText(Path.Combine(dir, "tracked.txt"), "v2\n");
            File.WriteAllText(Path.Combine(dir, "new-a.txt"), "a");
            File.WriteAllText(Path.Combine(dir, "new-b.txt"), "b");

            var result = _service.StageAll(dir, new[] { "tracked.txt", "new-a.txt", "new-b.txt" });

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);

            using var repo = new Repository(dir);
            Assert.Equal(FileStatus.ModifiedInIndex, repo.RetrieveStatus("tracked.txt"));
            Assert.Equal(FileStatus.NewInIndex, repo.RetrieveStatus("new-a.txt"));
            Assert.Equal(FileStatus.NewInIndex, repo.RetrieveStatus("new-b.txt"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StageAll_EmptyList_IsNoOpSuccess()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir);

            var result = _service.StageAll(dir, Array.Empty<string>());

            Assert.True(result.IsSuccess);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StageAll_NonExistentRepository_ReturnsFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zaide-mutation-nonexistent-" + Guid.NewGuid());

        var result = _service.StageAll(dir, new[] { "file.txt" });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Commit_EmptyMessage_ReturnsFailureWithoutGitCall()
    {
        var dir = CreateTempDir();
        try
        {
            Repository.Init(dir);

            var result = _service.Commit(dir, string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal("Commit message cannot be empty.", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Commit_NothingStaged_ReturnsFailure()
    {
        var dir = CreateTempDir();
        try
        {
            Repository.Init(dir);
            using var repo = new Repository(dir);
            repo.Config.Set("user.name", "Test User");
            repo.Config.Set("user.email", "test@example.com");

            var result = _service.Commit(dir, "test commit");

            Assert.False(result.IsSuccess);
            Assert.Equal("Nothing staged to commit.", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Commit_WithStagedFileAndValidMessage_Succeeds()
    {
        var dir = CreateTempDir();
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                repo.Config.Set("user.name", "Test User");
                repo.Config.Set("user.email", "test@example.com");
            }
            File.WriteAllText(Path.Combine(dir, "new.txt"), "content");
            using (var repo = new Repository(dir))
            {
                Commands.Stage(repo, "new.txt");
            }

            var result = _service.Commit(dir, "test commit");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.CommitSha);
            Assert.NotEmpty(result.CommitSha);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Commit_MissingGitIdentity_ReturnsFailure()
    {
        var dir = CreateTempDir();
        try
        {
            Repository.Init(dir);
            using (var repo = new Repository(dir))
            {
                repo.Config.Unset("user.name");
                repo.Config.Unset("user.email");
            }
            File.WriteAllText(Path.Combine(dir, "new.txt"), "content");
            using (var repo = new Repository(dir))
            {
                Commands.Stage(repo, "new.txt");
            }

            // Verify that BuildSignature actually returns null in this environment.
            // On developer machines with global git config, it may return the global
            // identity even after repo-level unset. The proof-of-concept tests
            // document this behavior. We only test the failure path when the
            // environment is clean (no identity at any config level).
            using (var repo = new Repository(dir))
            {
                var sig = repo.Config.BuildSignature(DateTimeOffset.Now);
                if (sig is not null)
                {
                    // Environment has global/system config — skip the failure
                    // assertion. The service correctly uses the available signature.
                    var result = _service.Commit(dir, "test commit");
                    Assert.True(result.IsSuccess);
                    return;
                }
            }

            var commitResult = _service.Commit(dir, "test commit");
            Assert.False(commitResult.IsSuccess);
            Assert.Contains("user identity", commitResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Commit_NonExistentRepository_ReturnsFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zaide-mutation-nonexistent-" + Guid.NewGuid());

        var result = _service.Commit(dir, "test commit");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Push_CleanTreeWithAheadCommits_Succeeds()
    {
        var bareDir = CreateTempDir();
        var localDir = CreateTempDir();
        try
        {
            Repository.Init(bareDir, isBare: true);
            Repository.Init(localDir);
            using (var local = new Repository(localDir))
            {
                local.Network.Remotes.Add("origin", bareDir);
                local.Config.Set("user.name", "Test");
                local.Config.Set("user.email", "test@example.com");
            }

            File.WriteAllText(Path.Combine(localDir, "seed.txt"), "seed\n");
            using (var local = new Repository(localDir))
            {
                Commands.Stage(local, "seed.txt");
                var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
                local.Commit("seed", author, author);
                local.Network.Push(local.Network.Remotes["origin"], local.Head.CanonicalName, new PushOptions());
            }
            ConfigureUpstreamAfterInitialPush(localDir);

            File.WriteAllText(Path.Combine(localDir, "seed.txt"), "seed\nmore\n");
            using (var local = new Repository(localDir))
            {
                Commands.Stage(local, "seed.txt");
                var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
                local.Commit("ahead", author, author);
            }

            var result = _service.Push(localDir);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);

            using var bare = new Repository(bareDir);
            Assert.Equal(2, bare.Commits.Count());
        }
        finally
        {
            Directory.Delete(bareDir, recursive: true);
            Directory.Delete(localDir, recursive: true);
        }
    }

    [Fact]
    public void Push_DirtyTree_ReturnsFailureWithoutPushing()
    {
        var bareDir = CreateTempDir();
        var localDir = CreateTempDir();
        try
        {
            Repository.Init(bareDir, isBare: true);
            Repository.Init(localDir);
            using (var local = new Repository(localDir))
            {
                local.Network.Remotes.Add("origin", bareDir);
                local.Config.Set("user.name", "Test");
                local.Config.Set("user.email", "test@example.com");
            }

            File.WriteAllText(Path.Combine(localDir, "seed.txt"), "seed\n");
            using (var local = new Repository(localDir))
            {
                Commands.Stage(local, "seed.txt");
                var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
                local.Commit("seed", author, author);
                local.Network.Push(local.Network.Remotes["origin"], local.Head.CanonicalName, new PushOptions());
            }
            ConfigureUpstreamAfterInitialPush(localDir);

            File.WriteAllText(Path.Combine(localDir, "dirty.txt"), "uncommitted\n");

            var result = _service.Push(localDir);

            Assert.False(result.IsSuccess);
            Assert.Equal("Cannot push with uncommitted changes.", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(bareDir, recursive: true);
            Directory.Delete(localDir, recursive: true);
        }
    }
}
