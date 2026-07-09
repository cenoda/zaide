using System;
using System.IO;
using LibGit2Sharp;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

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
}
