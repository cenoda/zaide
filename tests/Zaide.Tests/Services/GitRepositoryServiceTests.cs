using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Zaide.Models;
using Zaide.Services;
using Xunit;

namespace Zaide.Tests.Services;

public class GitRepositoryServiceTests
{
    private readonly IGitRepositoryService _service = new GitRepositoryService();

    private static string CreateTempDir()
    {
        return Directory.CreateTempSubdirectory("zaide-git-test-").FullName;
    }

    private static string InitRepoWithCommit(string path, string fileName = "file.txt")
    {
        Repository.Init(path);
        var repo = new Repository(path);
        var filePath = Path.Combine(path, fileName);
        File.WriteAllText(filePath, "hello");
        Commands.Stage(repo, filePath);
        var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
        repo.Commit("initial", author, author);
        return filePath;
    }

    [Fact]
    public void Discover_RepositoryFound_ReturnsRoot()
    {
        var path = CreateTempDir();
        try
        {
            Repository.Init(path);

            var result = _service.Discover(path);

            Assert.True(result.IsRepository);
            Assert.NotNull(result.RepositoryRoot);
            Assert.Contains(".git", result.RepositoryRoot!);
            Assert.Equal(path, result.StartingPath);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Discover_RepositoryMissing_ReturnsNotFound()
    {
        var path = CreateTempDir();
        try
        {
            var result = _service.Discover(path);

            Assert.False(result.IsRepository);
            Assert.Null(result.RepositoryRoot);
            Assert.Equal(path, result.StartingPath);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ReadStatus_CurrentBranch_IsMarkedCurrent()
    {
        var path = CreateTempDir();
        try
        {
            InitRepoWithCommit(path);
            var repo = new Repository(path);
            var expectedBranch = repo.Head.FriendlyName;

            var result = _service.Discover(path);
            Assert.True(result.IsRepository);

            var snapshot = _service.ReadStatus(result.RepositoryRoot!);

            Assert.Equal(expectedBranch, snapshot.CurrentBranchName);
            Assert.False(snapshot.IsDetachedHead);
            var current = Assert.Single(snapshot.Branches, b => b.IsCurrent);
            Assert.Equal(expectedBranch, current.Name);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ReadStatus_DetachedHead_ReportsDetachedWithSha()
    {
        var path = CreateTempDir();
        try
        {
            InitRepoWithCommit(path);
            var repo = new Repository(path);
            var commitSha = repo.Head.Tip.Sha;
            // Detach HEAD by checking out the commit directly.
            Commands.Checkout(repo, commitSha, new CheckoutOptions());

            var result = _service.Discover(path);
            var snapshot = _service.ReadStatus(result.RepositoryRoot!);

            Assert.True(snapshot.IsDetachedHead);
            Assert.Equal(commitSha, snapshot.CurrentBranchName);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ReadStatus_FileStatusMapping_AddedModifiedDeleted()
    {
        var path = CreateTempDir();
        try
        {
            var modifyPath = InitRepoWithCommit(path, "modify.txt");
            var repo = new Repository(path);
            // Second committed file to delete later
            var deletePath = Path.Combine(path, "delete.txt");
            File.WriteAllText(deletePath, "bye");
            Commands.Stage(repo, deletePath);
            var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("second", author, author);

            // Modified committed file
            File.WriteAllText(modifyPath, "changed");
            // Added new file
            var addedPath = Path.Combine(path, "added.txt");
            File.WriteAllText(addedPath, "new");
            // Deleted committed file
            File.Delete(deletePath);

            var result = _service.Discover(path);
            var snapshot = _service.ReadStatus(result.RepositoryRoot!);

            var changes = snapshot.Changes.ToList();
            Assert.Contains(changes, c => c.FilePath == "delete.txt" && c.ChangeType == GitChangeType.Deleted);
            Assert.Contains(changes, c => c.FilePath == "added.txt" && c.ChangeType == GitChangeType.Added);
            Assert.Contains(changes, c => c.FilePath == "modify.txt" && c.ChangeType == GitChangeType.Modified);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}