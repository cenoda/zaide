using System;
using System.IO;
using LibGit2Sharp;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;
using Xunit;

namespace Zaide.Tests.Features.SourceControl.Infrastructure;

/// <summary>
/// Repo-backed tests for <see cref="FileDiffService"/>. Each test creates a
/// temporary git repository, sets up the appropriate file state, calls the
/// service, and verifies the diff result.
/// </summary>
public class FileDiffServiceTests
{
    private static string CreateTempDir()
    {
        return Directory.CreateTempSubdirectory("zaide-diff-test-").FullName;
    }

    /// <summary>
    /// Initializes a repo, creates <paramref name="fileName"/> with content,
    /// stages it, and commits. Returns the repository root path.
    /// </summary>
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

    private readonly IFileDiffService _service = new FileDiffService();

    // ---------------------------------------------------------------
    //  Unstaged scenarios
    // ---------------------------------------------------------------

    [Fact]
    public void GetDiff_UnstagedModifiedFile_ReturnsDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\nline2\nline3\n");
            using var repo = new Repository(dir);

            // Modify without staging
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nmodified\nline3\n");

            var result = _service.GetDiff(
                dir,
                new FileChange("modify.txt", GitChangeType.Modified, isStaged: false));

            Assert.NotNull(result);
            Assert.Equal("modify.txt", result.FilePath);
            Assert.False(result.IsBinary);
            Assert.NotNull(result.DiffText);
            Assert.Contains("diff --git a/modify.txt b/modify.txt", result.DiffText);
            Assert.Contains("--- a/modify.txt", result.DiffText);
            Assert.Contains("+++ b/modify.txt", result.DiffText);
            Assert.Contains("@@", result.DiffText);
            Assert.Contains("-line2", result.DiffText);
            Assert.Contains("+modified", result.DiffText);
            Assert.True(result.AddedLines > 0);
            Assert.True(result.DeletedLines > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetDiff_UnstagedAddedFile_ReturnsDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "existing.txt");
            using var repo = new Repository(dir);

            // Create a new untracked file
            File.WriteAllText(Path.Combine(dir, "added.txt"), "new file content");

            var result = _service.GetDiff(
                dir,
                new FileChange("added.txt", GitChangeType.Added, isStaged: false));

            Assert.NotNull(result);
            Assert.False(result.IsBinary);
            Assert.NotNull(result.DiffText);
            Assert.Contains("diff --git a/added.txt b/added.txt", result.DiffText);
            Assert.Contains("--- /dev/null", result.DiffText);
            Assert.Contains("+++ b/added.txt", result.DiffText);
            Assert.Contains("+new file content", result.DiffText);
            Assert.True(result.AddedLines > 0);
            Assert.Equal(0, result.DeletedLines);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetDiff_UnstagedDeletedFile_ReturnsDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "delete.txt", "delete me");
            using var repo = new Repository(dir);

            // Delete the file
            File.Delete(Path.Combine(dir, "delete.txt"));

            var result = _service.GetDiff(
                dir,
                new FileChange("delete.txt", GitChangeType.Deleted, isStaged: false));

            Assert.NotNull(result);
            Assert.False(result.IsBinary);
            Assert.NotNull(result.DiffText);
            Assert.Contains("diff --git a/delete.txt b/delete.txt", result.DiffText);
            Assert.Contains("--- a/delete.txt", result.DiffText);
            Assert.Contains("+++ /dev/null", result.DiffText);
            Assert.Contains("-delete me", result.DiffText);
            Assert.Equal(0, result.AddedLines);
            Assert.True(result.DeletedLines > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Staged scenarios
    // ---------------------------------------------------------------

    [Fact]
    public void GetDiff_StagedModifiedFile_ReturnsDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "stage.txt", "original\ncontent\n");
            using var repo = new Repository(dir);

            // Modify and stage
            File.WriteAllText(Path.Combine(dir, "stage.txt"), "modified\ncontent\n");
            Commands.Stage(repo, "stage.txt");

            var result = _service.GetDiff(
                dir,
                new FileChange("stage.txt", GitChangeType.Modified, isStaged: true));

            Assert.NotNull(result);
            Assert.False(result.IsBinary);
            Assert.NotNull(result.DiffText);
            Assert.Contains("diff --git a/stage.txt b/stage.txt", result.DiffText);
            Assert.Contains("--- a/stage.txt", result.DiffText);
            Assert.Contains("+++ b/stage.txt", result.DiffText);
            Assert.Contains("@@", result.DiffText);
            Assert.Contains("-original", result.DiffText);
            Assert.Contains("+modified", result.DiffText);
            Assert.True(result.AddedLines > 0);
            Assert.True(result.DeletedLines > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Binary file scenario
    // ---------------------------------------------------------------

    [Fact]
    public void GetDiff_BinaryFile_ReturnsIsBinaryTrue()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "text.txt", "text file");
            using var repo = new Repository(dir);

            // Create a binary file (contains null byte)
            var binPath = Path.Combine(dir, "binary.bin");
            var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF };
            File.WriteAllBytes(binPath, binaryContent);

            // Stage it so TreeChanges can detect binary status
            Commands.Stage(repo, "binary.bin");
            var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("add binary", author, author);

            // Modify the binary file (write different binary content)
            File.WriteAllBytes(binPath, new byte[] { 0xFF, 0xFE, 0x00 });

            var result = _service.GetDiff(
                dir,
                new FileChange("binary.bin", GitChangeType.Modified, isStaged: false));

            Assert.NotNull(result);
            Assert.Equal("binary.bin", result.FilePath);
            Assert.True(result.IsBinary);
            Assert.Null(result.DiffText);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Unknown path scenario
    // ---------------------------------------------------------------

    [Fact]
    public void GetDiff_UnknownPath_ReturnsNull()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "known.txt");

            var result = _service.GetDiff(
                dir,
                new FileChange("nonexistent.txt", GitChangeType.Modified, isStaged: false));

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetDiff_EmptyFilePath_ReturnsNull()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "known.txt");

            var result = _service.GetDiff(
                dir,
                new FileChange("", GitChangeType.Modified, isStaged: false));

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetDiff_StagedDeletedFile_ReturnsDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "todelete.txt", "will be deleted");
            using var repo = new Repository(dir);

            // Delete and stage the deletion
            File.Delete(Path.Combine(dir, "todelete.txt"));
            Commands.Stage(repo, "todelete.txt");

            var result = _service.GetDiff(
                dir,
                new FileChange("todelete.txt", GitChangeType.Deleted, isStaged: true));

            Assert.NotNull(result);
            Assert.False(result.IsBinary);
            Assert.NotNull(result.DiffText);
            Assert.Contains("diff --git a/todelete.txt b/todelete.txt", result.DiffText);
            Assert.Contains("--- a/todelete.txt", result.DiffText);
            Assert.Contains("+++ /dev/null", result.DiffText);
            Assert.Contains("-will be deleted", result.DiffText);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
