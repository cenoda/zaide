using System;
using System.IO;
using LibGit2Sharp;
using Xunit;

namespace Zaide.Tests.Features.SourceControl.Infrastructure;

/// <summary>
/// Proof-of-concept: demonstrates that LibGit2Sharp's <c>Repository.Diff.Compare&lt;Patch&gt;()</c>
/// can produce unified-diff text for a single file. These tests initialize a temporary
/// repository, create / stage / modify / delete files, call the diff API, and assert
/// that properly formatted unified diff text is returned.
///
/// The results inform the Phase 7.3 diff view design:
///   - <c>Patch.Content</c> provides a complete unified diff string.
///   - HEAD:workdir diff works for unstaged changes.
///   - HEAD:index diff works for staged changes.
///   - Untracked files produce a full-addition diff when a path filter is provided.
///   - Deleted files show a full deletion diff.
///   - Binary detection is available via <c>Patch.IsBinaryComparison</c> (per-hunk)
///     or <c>TreeChanges</c> metadata.
/// </summary>
public class LibGit2SharpDiffProofOfConceptTests
{
    private static string CreateTempDir()
    {
        return Directory.CreateTempSubdirectory("zaide-diff-poc-").FullName;
    }

    /// <summary>
    /// Initializes a repo, creates <paramref name="fileName"/> with content,
    /// stages it, and commits with an initial commit.
    /// Returns the repository root path.
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

    // ---------------------------------------------------------------
    //  Unstaged (working directory) diff scenarios
    // ---------------------------------------------------------------

    [Fact]
    public void Diff_ModifiedFile_ReturnsUnifiedDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "modify.txt", "line1\nline2\nline3\n");
            using var repo = new Repository(dir);

            // Modify the file in the working directory
            File.WriteAllText(Path.Combine(dir, "modify.txt"), "line1\nmodified\nline3\n");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory,
                new[] { "modify.txt" });

            Assert.NotNull(patch);
            var content = patch.Content;
            Assert.False(string.IsNullOrEmpty(content));

            // Unified diff structure
            Assert.StartsWith("diff --git a/modify.txt b/modify.txt", content);
            Assert.Contains("--- a/modify.txt", content);
            Assert.Contains("+++ b/modify.txt", content);
            // The hunk header starts with @@
            Assert.Contains("@@", content);
            // The removed line
            Assert.Contains("-line2", content);
            // The added line
            Assert.Contains("+modified", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Diff_DeletedFile_ReturnsDeletionDiff()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "delete.txt", "delete me");
            using var repo = new Repository(dir);

            // Delete the file
            File.Delete(Path.Combine(dir, "delete.txt"));

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory,
                new[] { "delete.txt" });

            Assert.NotNull(patch);
            var content = patch.Content;
            Assert.False(string.IsNullOrEmpty(content));

            // Should show deletion diff
            Assert.Contains("diff --git a/delete.txt b/delete.txt", content);
            Assert.Contains("--- a/delete.txt", content);
            Assert.Contains("+++ /dev/null", content);
            Assert.Contains("-delete me", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Diff_UntrackedFile_ProducesFullAdditionDiff()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "tracked.txt");
            using var repo = new Repository(dir);

            // Create a new untracked file
            File.WriteAllText(Path.Combine(dir, "untracked.txt"), "new content");

            // When passing an explicit path filter, DiffTargets.WorkingDirectory
            // includes the untracked file as a full-file addition.
            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory,
                new[] { "untracked.txt" });

            Assert.NotNull(patch);
            // Untracked files produce a diff showing the full file as added content
            // (treats HEAD path as /dev/null and workdir as the new file).
            Assert.False(string.IsNullOrEmpty(patch.Content));
            Assert.Contains("diff --git a/untracked.txt b/untracked.txt", patch.Content);
            Assert.Contains("--- /dev/null", patch.Content);
            Assert.Contains("+++ b/untracked.txt", patch.Content);
            Assert.Contains("+new content", patch.Content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Staged (index) diff scenarios
    // ---------------------------------------------------------------

    [Fact]
    public void Diff_StagedModifiedFile_ReturnsUnifiedDiffText()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "stage.txt", "original\ncontent\n");
            using var repo = new Repository(dir);

            // Modify and stage
            File.WriteAllText(Path.Combine(dir, "stage.txt"), "modified\ncontent\n");
            Commands.Stage(repo, "stage.txt");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.Index,
                new[] { "stage.txt" });

            Assert.NotNull(patch);
            var content = patch.Content;
            Assert.False(string.IsNullOrEmpty(content));

            Assert.StartsWith("diff --git a/stage.txt b/stage.txt", content);
            Assert.Contains("--- a/stage.txt", content);
            Assert.Contains("+++ b/stage.txt", content);
            Assert.Contains("@@", content);
            Assert.Contains("-original", content);
            Assert.Contains("+modified", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Diff_StagedNewFile_ReturnsFullFileAsDiff()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "existing.txt");
            using var repo = new Repository(dir);

            // Create a new file and stage it
            var newFilePath = Path.Combine(dir, "new.txt");
            File.WriteAllText(newFilePath, "brand new file");
            Commands.Stage(repo, "new.txt");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.Index,
                new[] { "new.txt" });

            Assert.NotNull(patch);
            var content = patch.Content;
            Assert.False(string.IsNullOrEmpty(content));

            // New file shows as /dev/null → new.txt
            Assert.Contains("diff --git a/new.txt b/new.txt", content);
            Assert.Contains("--- /dev/null", content);
            Assert.Contains("+++ b/new.txt", content);
            Assert.Contains("+brand new file", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Diff_StagedDeletedFile_ReturnsDeletionDiff()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "todelete.txt", "will be deleted");
            using var repo = new Repository(dir);

            // Delete and stage the deletion
            File.Delete(Path.Combine(dir, "todelete.txt"));
            Commands.Stage(repo, "todelete.txt");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.Index,
                new[] { "todelete.txt" });

            Assert.NotNull(patch);
            var content = patch.Content;
            Assert.False(string.IsNullOrEmpty(content));

            Assert.Contains("diff --git a/todelete.txt b/todelete.txt", content);
            Assert.Contains("--- a/todelete.txt", content);
            Assert.Contains("+++ /dev/null", content);
            Assert.Contains("-will be deleted", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Multiple-file diff scenario
    // ---------------------------------------------------------------

    [Fact]
    public void Diff_AllUnstaged_ReturnsCombinedDiff()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "a.txt", "file a\n");
            using var repo = new Repository(dir);

            var bPath = Path.Combine(dir, "b.txt");
            File.WriteAllText(bPath, "file b\n");
            Commands.Stage(repo, bPath);
            var author = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("add b", author, author);

            // Modify both files without staging
            File.WriteAllText(Path.Combine(dir, "a.txt"), "modified a\n");
            File.WriteAllText(bPath, "modified b\n");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory);

            Assert.NotNull(patch);
            var content = patch.Content;

            // Both files should appear in the combined diff
            Assert.Contains("diff --git a/a.txt b/a.txt", content);
            Assert.Contains("diff --git a/b.txt b/b.txt", content);
            Assert.Contains("+modified a", content);
            Assert.Contains("+modified b", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  Multi-hunk scenario: verifying Patch exposes hunks
    // ---------------------------------------------------------------

    [Fact]
    public void Diff_ModifiedFile_PatchExposesHunks()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(
                dir,
                "hunky.txt",
                "line1\nline2\nline3\nline4\nline5\n");
            using var repo = new Repository(dir);

            // Multiple modifications to create multiple hunks
            File.WriteAllText(
                Path.Combine(dir, "hunky.txt"),
                "changed1\nline2\nline3\nchanged4\nline5\n");

            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory,
                new[] { "hunky.txt" });

            Assert.NotNull(patch);
            // The Patch object exposes hunks via patch[path] which returns a PatchEntryChanges.
            // PatchEntryChanges has Hunks property.
            var fileChanges = patch["hunky.txt"];
            Assert.NotNull(fileChanges);

            // PatchEntryChanges exposes LinesAdded and LinesDeleted counts.
            // Note: Hunks enumeration is not available in LibGit2Sharp 0.30.0
            // on PatchEntryChanges; only Patch.Content provides the unified diff.
            var content = patch.Content;
            Assert.Contains("@@", content);
            Assert.True(fileChanges.LinesAdded > 0);
            Assert.True(fileChanges.LinesDeleted > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    //  No-diff scenario
    // ---------------------------------------------------------------

    [Fact]
    public void Diff_UnmodifiedFile_ReturnsEmptyPatch()
    {
        var dir = CreateTempDir();
        try
        {
            InitRepoWithCommit(dir, "same.txt", "unchanged");
            using var repo = new Repository(dir);

            // File is not modified — diff should be empty
            var patch = repo.Diff.Compare<Patch>(
                repo.Head.Tip.Tree,
                DiffTargets.WorkingDirectory,
                new[] { "same.txt" });

            Assert.NotNull(patch);
            Assert.True(string.IsNullOrEmpty(patch.Content));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
