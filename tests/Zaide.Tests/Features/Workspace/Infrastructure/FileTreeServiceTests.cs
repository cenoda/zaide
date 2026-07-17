using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Xunit;
using Zaide.Models;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;

namespace Zaide.Tests.Features.Workspace.Infrastructure;

public class FileTreeServiceTests
{
    private readonly FileTreeService _service = new();

    [Theory]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData("__pycache__")]
    [InlineData(".DS_Store")]
    [InlineData("Thumbs.db")]
    [InlineData(".hidden")]
    public void IsIgnored_ReturnsTrue_ForCommonPatterns(string name)
    {
        Assert.True(_service.IsIgnored(name));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("MyProject")]
    [InlineData("README.md")]
    [InlineData("Program.cs")]
    [InlineData("folder")]
    public void IsIgnored_ReturnsFalse_ForNormalFolders(string name)
    {
        Assert.False(_service.IsIgnored(name));
    }

    [Fact]
    public void EnumerateDirectory_ReturnsNestedTree_ForTempFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "subdir"));
            File.WriteAllText(Path.Combine(root, "readme.md"), "hello");
            File.WriteAllText(Path.Combine(root, "subdir", "nested.txt"), "world");

            var nodes = _service.EnumerateDirectory(root);

            Assert.Equal(2, nodes.Count);
            Assert.Equal("subdir", nodes[0].Name);
            Assert.True(nodes[0].IsDirectory);
            Assert.Single(nodes[0].Children);
            Assert.Equal("nested.txt", nodes[0].Children[0].Name);

            Assert.Equal("readme.md", nodes[1].Name);
            Assert.False(nodes[1].IsDirectory);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateDirectory_SkipsIgnoredDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            Directory.CreateDirectory(Path.Combine(root, "src"));
            File.WriteAllText(Path.Combine(root, "node_modules", "package.json"), "{}");
            File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "code");

            var nodes = _service.EnumerateDirectory(root);

            Assert.Single(nodes);
            Assert.Equal("src", nodes[0].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsHidden_ExcludesDotAndDotDot()
    {
        Assert.False(_service.IsIgnored("."));
        Assert.False(_service.IsIgnored(".."));
    }

    [Fact]
    public void EnumerateDirectory_SortsDirectoriesBeforeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "z-dir"));
            Directory.CreateDirectory(Path.Combine(root, "a-dir"));
            File.WriteAllText(Path.Combine(root, "z-file.txt"), "z");
            File.WriteAllText(Path.Combine(root, "a-file.txt"), "a");

            var nodes = _service.EnumerateDirectory(root);

            // Directories should come first, sorted alphabetically
            Assert.Equal("a-dir", nodes[0].Name);
            Assert.Equal("z-dir", nodes[1].Name);
            // Files should come after directories, sorted alphabetically
            Assert.Equal("a-file.txt", nodes[2].Name);
            Assert.Equal("z-file.txt", nodes[3].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // M3.4: depth tagging
    [Fact]
    public void EnumerateDirectory_RootChildren_HaveDepthZero()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllText(Path.Combine(root, "file.txt"), "x");

            var nodes = _service.EnumerateDirectory(root);

            Assert.All(nodes, n => Assert.Equal(0, n.Depth));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateDirectory_NestedChildren_HaveIncreasingDepth()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            var level1 = Directory.CreateDirectory(Path.Combine(root, "level1"));
            var level2 = level1.CreateSubdirectory("level2");
            var level3 = level2.CreateSubdirectory("level3");
            File.WriteAllText(Path.Combine(level3.FullName, "deep.txt"), "x");
            File.WriteAllText(Path.Combine(level2.FullName, "mid.txt"), "x");
            File.WriteAllText(Path.Combine(level1.FullName, "top.txt"), "x");

            var nodes = _service.EnumerateDirectory(root);

            var level1Node = nodes.First(n => n.Name == "level1");
            Assert.Equal(0, level1Node.Depth);

            var level2Node = level1Node.Children.First(n => n.Name == "level2");
            Assert.Equal(1, level2Node.Depth);

            var level3Node = level2Node.Children.First(n => n.Name == "level3");
            Assert.Equal(2, level3Node.Depth);

            var deepFile = level3Node.Children.First(n => n.Name == "deep.txt");
            Assert.Equal(3, deepFile.Depth);

            var midFile = level2Node.Children.First(n => n.Name == "mid.txt");
            Assert.Equal(2, midFile.Depth);

            var topFile = level1Node.Children.First(n => n.Name == "top.txt");
            Assert.Equal(1, topFile.Depth);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Live FileSystemWatcher integration tests ──────────────────────────
    //
    // These tests use polling-based collection rather than async/await
    // because FileSystemWatcher on Linux may fire events on OS threads
    // that don't interact with xUnit's synchronization context.

    [Fact]
    public void StartWatching_EmitsCreatedEvent_WhenFileCreated()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var collected = new System.Collections.Generic.List<FileChangeEvent>();
            var fileChanges = _service.StartWatching(root);
            using var _ = fileChanges.Subscribe(collected.Add);

            // Brief settle period for the watcher to initialise on the OS
            System.Threading.Thread.Sleep(300);

            var filePath = Path.Combine(root, "external-file.txt");
            File.WriteAllText(filePath, "content");

            // Poll up to 5 seconds for the Created event
            var deadline = DateTime.UtcNow.AddSeconds(5);
            FileChangeEvent? found = null;
            while (DateTime.UtcNow < deadline)
            {
                found = collected.FirstOrDefault(e => e.Type == ChangeType.Created && e.FullPath == filePath);
                if (found is not null) break;
                System.Threading.Thread.Sleep(100);
            }
            _service.StopWatching();

            Assert.NotNull(found);
            Assert.Equal(ChangeType.Created, found!.Type);
            Assert.Equal(filePath, found.FullPath);
        }
        finally
        {
            _service.StopWatching();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartWatching_EmitsChangedEvent_WhenFileModified()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var filePath = Path.Combine(root, "file.txt");
            File.WriteAllText(filePath, "initial");

            var collected = new System.Collections.Generic.List<FileChangeEvent>();
            var fileChanges = _service.StartWatching(root);
            using var _ = fileChanges.Subscribe(collected.Add);

            System.Threading.Thread.Sleep(300);

            // Modify the file
            File.WriteAllText(filePath, "modified");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            FileChangeEvent? found = null;
            while (DateTime.UtcNow < deadline)
            {
                found = collected.FirstOrDefault(e => e.Type == ChangeType.Changed && e.FullPath == filePath);
                if (found is not null) break;
                System.Threading.Thread.Sleep(100);
            }
            _service.StopWatching();

            Assert.NotNull(found);
            Assert.Equal(ChangeType.Changed, found!.Type);
            Assert.Equal(filePath, found.FullPath);
        }
        finally
        {
            _service.StopWatching();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartWatching_EmitsDeletedEvent_WhenFileDeleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var filePath = Path.Combine(root, "to-delete.txt");
            File.WriteAllText(filePath, "delete me");

            var collected = new System.Collections.Generic.List<FileChangeEvent>();
            var fileChanges = _service.StartWatching(root);
            using var _ = fileChanges.Subscribe(collected.Add);

            System.Threading.Thread.Sleep(300);

            File.Delete(filePath);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            FileChangeEvent? found = null;
            while (DateTime.UtcNow < deadline)
            {
                found = collected.FirstOrDefault(e => e.Type == ChangeType.Deleted && e.FullPath == filePath);
                if (found is not null) break;
                System.Threading.Thread.Sleep(100);
            }
            _service.StopWatching();

            Assert.NotNull(found);
            Assert.Equal(ChangeType.Deleted, found!.Type);
            Assert.Equal(filePath, found.FullPath);
        }
        finally
        {
            _service.StopWatching();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartWatching_EmitsRenamedEvent_WhenFileRenamed()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var oldPath = Path.Combine(root, "old-name.txt");
            var newPath = Path.Combine(root, "new-name.txt");
            File.WriteAllText(oldPath, "rename me");

            var collected = new System.Collections.Generic.List<FileChangeEvent>();
            var fileChanges = _service.StartWatching(root);
            using var _ = fileChanges.Subscribe(collected.Add);

            System.Threading.Thread.Sleep(300);

            File.Move(oldPath, newPath);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            FileChangeEvent? found = null;
            while (DateTime.UtcNow < deadline)
            {
                found = collected.FirstOrDefault(e => e.Type == ChangeType.Renamed && e.FullPath == newPath);
                if (found is not null) break;
                System.Threading.Thread.Sleep(100);
            }
            _service.StopWatching();

            Assert.NotNull(found);
            Assert.Equal(ChangeType.Renamed, found!.Type);
            Assert.Equal(newPath, found.FullPath);
            Assert.Equal(oldPath, found.OldPath);
        }
        finally
        {
            _service.StopWatching();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartWatching_BuffersRapidChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var collected = new System.Collections.Generic.List<FileChangeEvent>();
            var fileChanges = _service.StartWatching(root);
            using var _ = fileChanges.Subscribe(collected.Add);

            System.Threading.Thread.Sleep(300);

            // Rapidly create 5 files
            for (var i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(root, $"rapid-{i}.txt"), $"content-{i}");

            // Poll for up to 10 seconds
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var createdCount = collected.Count(e => e.Type == ChangeType.Created);
                if (createdCount >= 5) break;
                System.Threading.Thread.Sleep(100);
            }
            _service.StopWatching();

            var created = collected.Where(e => e.Type == ChangeType.Created).ToList();
            Assert.True(created.Count >= 5, $"Expected at least 5 Created events, got {created.Count}");
            Assert.All(created, e => Assert.Equal(ChangeType.Created, e.Type));
        }
        finally
        {
            _service.StopWatching();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteFile_RemovesFileFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var filePath = Path.Combine(root, "to-delete.txt");
            File.WriteAllText(filePath, "content");

            _service.DeleteFile(filePath);

            Assert.False(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteDirectory_RemovesEmptyDirectoryFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "empty-dir");
            Directory.CreateDirectory(dirPath);

            _service.DeleteDirectory(dirPath);

            Assert.False(Directory.Exists(dirPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteDirectory_RemovesDirectoryAndContentsRecursively()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "nested-dir");
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, "child.txt"), "content");
            Directory.CreateDirectory(Path.Combine(dirPath, "sub"));
            File.WriteAllText(Path.Combine(dirPath, "sub", "grandchild.txt"), "nested");

            _service.DeleteDirectory(dirPath);

            Assert.False(Directory.Exists(dirPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
