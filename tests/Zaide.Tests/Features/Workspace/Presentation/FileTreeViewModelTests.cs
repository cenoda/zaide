using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Tests.Features.Workspace.Presentation;

public class FileTreeViewModelTests
{
    static FileTreeViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private readonly FileTreeService _service = new();
    private readonly IScheduler _scheduler = CurrentThreadScheduler.Instance;

    [Fact]
    public void RootNodes_IsEmpty_BeforeFolderOpened()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void OpenFolderCommand_PopulatesRootNodes_ForTempDirectory()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "app.js"), "code");
            File.WriteAllText(Path.Combine(root, "style.css"), "body {}");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });

            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Equal(root, vm.RootPath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelectedFile_DefaultsToNull()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void HandleRenamed_UpdatesDescendantPaths()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dir = Directory.CreateDirectory(Path.Combine(root, "subdir"));
            File.WriteAllText(Path.Combine(dir.FullName, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(dir.FullName, "file2.txt"), "content2");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var subdirNode = vm.RootNodes.FirstOrDefault(n => n.Name == "subdir");
            Assert.NotNull(subdirNode);

            var oldDirPath = subdirNode.FullPath;
            var newDirPath = Path.Combine(root, "renamed_subdir");
            Directory.Move(oldDirPath, newDirPath);

            vm.HandleRenamed(newDirPath, oldDirPath);

            var renamedNode = vm.RootNodes.FirstOrDefault(n => n.Name == "renamed_subdir");
            Assert.NotNull(renamedNode);
            Assert.Equal(newDirPath, renamedNode.FullPath);
            Assert.Equal(2, renamedNode.Children.Count);
            Assert.True(renamedNode.Children.All(c => c.FullPath.StartsWith(newDirPath)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleRenamed_UpdatesDescendantPaths_WithNonAscii()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dir = Directory.CreateDirectory(Path.Combine(root, "한글_😊"));
            File.WriteAllText(Path.Combine(dir.FullName, "file.txt"), "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var subdirNode = vm.RootNodes.FirstOrDefault(n => n.Name == "한글_😊");
            Assert.NotNull(subdirNode);

            var oldDirPath = subdirNode.FullPath;
            var newDirPath = Path.Combine(root, "renamed_한글");
            Directory.Move(oldDirPath, newDirPath);

            vm.HandleRenamed(newDirPath, oldDirPath);

            var renamedNode = vm.RootNodes.FirstOrDefault(n => n.Name == "renamed_한글");
            Assert.NotNull(renamedNode);
            Assert.Equal(newDirPath, renamedNode.FullPath);
            Assert.Single(renamedNode.Children);
            Assert.True(renamedNode.Children.All(c => c.FullPath.StartsWith(newDirPath)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleRenamed_DoesNotCorruptPaths_WithPartialNameMatch()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dir = Directory.CreateDirectory(Path.Combine(root, "proj"));
            var subdir = Directory.CreateDirectory(Path.Combine(dir.FullName, "backup"));
            File.WriteAllText(Path.Combine(subdir.FullName, "old_project.txt"), "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var projNode = vm.RootNodes.FirstOrDefault(n => n.Name == "proj");
            Assert.NotNull(projNode);

            var oldDirPath = projNode.FullPath;
            var newDirPath = Path.Combine(root, "renamed_proj");
            Directory.Move(oldDirPath, newDirPath);

            vm.HandleRenamed(newDirPath, oldDirPath);

            var renamedNode = vm.RootNodes.FirstOrDefault(n => n.Name == "renamed_proj");
            Assert.NotNull(renamedNode);
            Assert.Equal(newDirPath, renamedNode.FullPath);
            Assert.Single(renamedNode.Children);
            var backupNode = renamedNode.Children.FirstOrDefault(c => c.Name == "backup");
            Assert.NotNull(backupNode);
            Assert.StartsWith(newDirPath, backupNode.FullPath);
            Assert.Contains("old_project.txt", backupNode.Children.FirstOrDefault()?.Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenFolderCommand_SetsStatusText_OnInaccessiblePath()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var inaccessiblePath = "/nonexistent/path/that/does/not/exist";

        vm.OpenFolderCommand.Execute(inaccessiblePath).Subscribe(_ => { });

        Assert.NotNull(vm.StatusText);
        Assert.Contains("Directory not found", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void OpenFolderCommand_SetsStatusText_OnFilePath()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var filePath = Path.GetTempFileName();

        try
        {
            vm.OpenFolderCommand.Execute(filePath).Subscribe(_ => { });

            Assert.NotNull(vm.StatusText);
            Assert.Contains("Directory not found", vm.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(vm.RootNodes);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenFolderCommand_SetsStatusText_OnInvalidPath()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var invalidPath = "invalid\0path";

        vm.OpenFolderCommand.Execute(invalidPath).Subscribe(_ => { });

        Assert.NotNull(vm.StatusText);
        Assert.Contains("Invalid Argument", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void SetRootPath_Null_ClearsTreeAndSelection()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "file.txt"), "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Single(vm.RootNodes);
            Assert.Equal(root, vm.RootPath);

            vm.SelectedFile = vm.RootNodes[0];

            vm.SetRootPath(null);

            Assert.Null(vm.RootPath);
            Assert.Empty(vm.RootNodes);
            Assert.Null(vm.SelectedFile);
            Assert.Null(vm.StatusText);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetRootPath_Null_CallsStopWatching()
    {
        var mockService = new Moq.Mock<IFileTreeService>();
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new List<FileTreeNode>());
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(System.Reactive.Linq.Observable.Never<FileChangeEvent>());

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);

        vm.SetRootPath("/fake/path");
        vm.SetRootPath(null);

        mockService.Verify(s => s.StopWatching(), Moq.Times.AtLeastOnce);
    }

    [Fact]
    public void FailedOpen_PreservesPriorTreeAndWatcherState()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "app.js"), "code");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Single(vm.RootNodes);
            Assert.Equal(root, vm.RootPath);

            vm.OpenFolderCommand.Execute("/nonexistent/path").Subscribe(_ => { });

            Assert.Equal(root, vm.RootPath);
            Assert.Single(vm.RootNodes);
            Assert.NotNull(vm.StatusText);
            Assert.Contains("Directory not found", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetRootPath_Null_DisposesWatcherSubscription()
    {
        var mockService = new Moq.Mock<IFileTreeService>();
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new List<FileTreeNode>());
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(System.Reactive.Linq.Observable.Never<FileChangeEvent>());

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);

        // Open a folder to create a watcher subscription
        vm.SetRootPath("/fake/path");

        // Close — should dispose the subscription and call StopWatching
        vm.SetRootPath(null);

        mockService.Verify(s => s.StopWatching(), Moq.Times.AtLeastOnce);
        Assert.Null(vm.RootPath);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public async System.Threading.Tasks.Task CloseFolderRequested_CompletesWhenNoFolderOpen()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        Assert.Null(vm.RootPath);

        // Register a handler that just completes
        using var sub = vm.CloseFolderRequested.RegisterHandler(interaction =>
        {
            interaction.SetOutput(Unit.Default);
            return System.Threading.Tasks.Task.CompletedTask;
        });

        // Should complete without hanging even though no folder is open
        var result = await vm.CloseFolderRequested.Handle(Unit.Default).FirstAsync().ToTask();
        Assert.Equal(Unit.Default, result);
    }

    // ── Live-sync tests ─────────────────────────────────────────────────────

    [Fact]
    public void HandleCreated_SetsDepthCorrectly_ForRootLevel()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Empty(vm.RootNodes);

            // Simulate a file creation at root level
            var newFilePath = Path.Combine(root, "newfile.txt");
            File.WriteAllText(newFilePath, "content");
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, newFilePath));

            Assert.Single(vm.RootNodes);
            Assert.Equal(0, vm.RootNodes[0].Depth);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleCreated_SetsDepthCorrectly_ForNestedFile()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var subdir = Directory.CreateDirectory(Path.Combine(root, "subdir"));
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });

            var subdirNode = vm.RootNodes.FirstOrDefault(n => n.Name == "subdir");
            Assert.NotNull(subdirNode);
            Assert.Equal(0, subdirNode.Depth);

            // Simulate a file creation inside subdir
            var newFilePath = Path.Combine(subdir.FullName, "nested.txt");
            File.WriteAllText(newFilePath, "content");
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, newFilePath));

            Assert.Single(subdirNode.Children);
            Assert.Equal(1, subdirNode.Children[0].Depth);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleCreated_MaintainsSortOrder_DirectoriesBeforeFiles()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "z-file.txt"), "z");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Single(vm.RootNodes);

            // Create a directory — should be inserted before the file
            var newDirPath = Path.Combine(root, "a-dir");
            Directory.CreateDirectory(newDirPath);
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, newDirPath));

            Assert.Equal(2, vm.RootNodes.Count);
            Assert.True(vm.RootNodes[0].IsDirectory);
            Assert.Equal("a-dir", vm.RootNodes[0].Name);
            Assert.False(vm.RootNodes[1].IsDirectory);
            Assert.Equal("z-file.txt", vm.RootNodes[1].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleCreated_MaintainsSortOrder_AlphabeticalWithinCategory()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "a-file.txt"), "a");
            File.WriteAllText(Path.Combine(root, "c-file.txt"), "c");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Equal(2, vm.RootNodes.Count);

            // Create a file that should be inserted between a-file and c-file
            var newFilePath = Path.Combine(root, "b-file.txt");
            File.WriteAllText(newFilePath, "b");
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, newFilePath));

            Assert.Equal(3, vm.RootNodes.Count);
            Assert.Equal("a-file.txt", vm.RootNodes[0].Name);
            Assert.Equal("b-file.txt", vm.RootNodes[1].Name);
            Assert.Equal("c-file.txt", vm.RootNodes[2].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleCreated_DoesNotAddDuplicate_WhenNodeAlreadyExists()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "existing.txt"), "content");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Single(vm.RootNodes);

            // Simulate a duplicate Created event for the same file
            var existingPath = Path.Combine(root, "existing.txt");
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, existingPath));

            // Should still be only one node — no duplicate
            Assert.Single(vm.RootNodes);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Refresh_ReEnumeratesTree_WithoutRestartingWatcher()
    {
        var mockService = new Moq.Mock<IFileTreeService>();
        var callCount = 0;
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(() =>
            {
                callCount++;
                return new List<FileTreeNode>
                {
                    new FileTreeNode { Name = $"file-{callCount}.txt", FullPath = $"/fake/file-{callCount}.txt", IsDirectory = false, Depth = 0 }
                };
            });
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(System.Reactive.Linq.Observable.Never<FileChangeEvent>());

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);
        vm.SetRootPath("/fake/path");
        Assert.Single(vm.RootNodes);
        Assert.Equal("file-1.txt", vm.RootNodes[0].Name);

        // Refresh should re-enumerate without calling StopWatching/StartWatching
        vm.Refresh();

        Assert.Single(vm.RootNodes);
        Assert.Equal("file-2.txt", vm.RootNodes[0].Name);
        // StopWatching was called once during SetRootPath, NOT again during Refresh
        mockService.Verify(s => s.StopWatching(), Moq.Times.Once);
        // StartWatching was called once during SetRootPath, not again during Refresh
        mockService.Verify(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Once);
    }

    [Fact]
    public void Refresh_IsNoOp_WhenNoFolderOpen()
    {
        var mockService = new Moq.Mock<IFileTreeService>();
        var vm = new FileTreeViewModel(mockService.Object, _scheduler);

        vm.Refresh();

        mockService.Verify(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Never);
    }

    [Fact]
    public void HandleDeleted_IsNoOp_WhenNodeDoesNotExist()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Empty(vm.RootNodes);

            // Deleting a non-existent file should not throw
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Deleted, Path.Combine(root, "ghost.txt")));
            Assert.Empty(vm.RootNodes);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SetRootPath_SwitchingWorkspaces_StopsOldWatcherAndStartsNew()
    {
        var mockService = new Moq.Mock<IFileTreeService>();
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new List<FileTreeNode>());
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(System.Reactive.Linq.Observable.Never<FileChangeEvent>());

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);

        vm.SetRootPath("/workspace-a");
        vm.SetRootPath("/workspace-b");

        // StopWatching called at least once during the transition
        mockService.Verify(s => s.StopWatching(), Moq.Times.AtLeastOnce);
        // StartWatching called for each workspace
        mockService.Verify(s => s.StartWatching("/workspace-a", It.IsAny<bool>()), Moq.Times.Once);
        mockService.Verify(s => s.StartWatching("/workspace-b", It.IsAny<bool>()), Moq.Times.Once);
    }

    // ── Re-sort after rename ──────────────────────────────────────────────

    [Fact]
    public void HandleRenamed_ReSortsAlphabetically_AfterRename()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "m-file.txt"), "middle");
            File.WriteAllText(Path.Combine(root, "z-file.txt"), "zzz");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Equal("m-file.txt", vm.RootNodes[0].Name);
            Assert.Equal("z-file.txt", vm.RootNodes[1].Name);

            // Rename z-file.txt to a-file.txt — should move to first position
            var oldPath = Path.Combine(root, "z-file.txt");
            var newPath = Path.Combine(root, "a-file.txt");
            File.Move(oldPath, newPath);
            vm.HandleRenamed(newPath, oldPath);

            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Equal("a-file.txt", vm.RootNodes[0].Name);
            Assert.Equal("m-file.txt", vm.RootNodes[1].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleRenamed_ReSortsDirectoriesBeforeFiles()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "adir"));
            File.WriteAllText(Path.Combine(root, "bfile.txt"), "content");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Equal(2, vm.RootNodes.Count);
            Assert.True(vm.RootNodes[0].IsDirectory);
            Assert.Equal("adir", vm.RootNodes[0].Name);

            // Rename the directory to start with 'z' — still directories before files
            var oldDirPath = Path.Combine(root, "adir");
            var newDirPath = Path.Combine(root, "zdir");
            Directory.Move(oldDirPath, newDirPath);
            vm.HandleRenamed(newDirPath, oldDirPath);

            Assert.Equal(2, vm.RootNodes.Count);
            Assert.True(vm.RootNodes[0].IsDirectory);
            Assert.Equal("zdir", vm.RootNodes[0].Name);
            Assert.False(vm.RootNodes[1].IsDirectory);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Deferred Created events ───────────────────────────────────────────

    [Fact]
    public void HandleCreated_DefersUntilParentAppears()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Empty(vm.RootNodes);

            // Simulate child file Created arriving before parent directory Created
            var subdir = Path.Combine(root, "subdir");
            var fileInSubdir = Path.Combine(subdir, "child.txt");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(fileInSubdir, "content");

            // Fire child first (parent node not yet in tree)
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, fileInSubdir));
            Assert.Empty(vm.RootNodes); // child deferred

            // Now fire parent directory Created — this also triggers RetryPendingEvents
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, subdir));

            // Both should now be present
            Assert.Single(vm.RootNodes);
            Assert.True(vm.RootNodes[0].IsDirectory);
            Assert.Equal("subdir", vm.RootNodes[0].Name);
            Assert.Single(vm.RootNodes[0].Children);
            Assert.Equal("child.txt", vm.RootNodes[0].Children[0].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Changed event routing ─────────────────────────────────────────────

    [Fact]
    public void HandleFileChange_RoutesChangedEvent()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "existing.txt"), "content");
            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            Assert.Single(vm.RootNodes);

            // Changed events should not throw and should not corrupt the tree
            var exception = Record.Exception(() =>
            {
                vm.HandleFileChange(new FileChangeEvent(ChangeType.Changed, Path.Combine(root, "existing.txt")));
                vm.HandleFileChange(new FileChangeEvent(ChangeType.Changed, Path.Combine(root, "nonexistent.txt")));
            });

            Assert.Null(exception);
            Assert.Single(vm.RootNodes); // tree unchanged
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Watcher restart subscription ──────────────────────────────────────

    [Fact]
    public void SetRootPath_SubscribesToRestartObservable()
    {
        var restartSubject = new System.Reactive.Subjects.Subject<System.Reactive.Unit>();
        var mockService = new Moq.Mock<IFileTreeService>();
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new List<FileTreeNode>
            {
                new FileTreeNode { Name = "file.txt", FullPath = "/fake/file.txt", IsDirectory = false, Depth = 0 }
            });
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(System.Reactive.Linq.Observable.Never<FileChangeEvent>());
        mockService.Setup(s => s.WhenWatcherRestarted)
            .Returns(restartSubject);

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);
        vm.SetRootPath("/fake/path");
        Assert.Single(vm.RootNodes);

        // Before restart: EnumerateDirectory called once, StartWatching called once
        mockService.Verify(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Once);
        mockService.Verify(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Once);

        // Simulate a watcher restart
        restartSubject.OnNext(System.Reactive.Unit.Default);

        // After restart: StartWatching called again (restarts observation),
        // EnumerateDirectory called again (via Refresh to reconcile missed events)
        mockService.Verify(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Exactly(2));
        mockService.Verify(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()), Moq.Times.Exactly(2));
    }

    [Fact]
    public void SetRootPath_NormalizesTrailingDirectorySeparator()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-norm-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var pathWithSlash = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var expected = FileTreeViewModel.NormalizeDirectoryPath(pathWithSlash);
            Assert.False(
                expected.EndsWith(Path.DirectorySeparatorChar) || expected.EndsWith(Path.AltDirectorySeparatorChar),
                "Normalized path should not end with a separator (except filesystem root)");

            var vm = new FileTreeViewModel(_service, _scheduler);
            Assert.True(vm.SetRootPath(pathWithSlash));
            Assert.Equal(expected, vm.RootPath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HandleCreated_RootLevel_WithTrailingSlashInput_AddsNode()
    {
        // Regression: folder pickers yield trailing separators; FSW FullPath
        // parents from Path.GetDirectoryName never do. Live sync must still match.
        var root = Path.Combine(Path.GetTempPath(), "zaide-trail-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var vm = new FileTreeViewModel(_service, _scheduler);
            var pathWithSlash = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.True(vm.SetRootPath(pathWithSlash));

            var filePath = Path.Combine(
                root.TrimEnd(Path.DirectorySeparatorChar),
                "new.txt");
            File.WriteAllText(filePath, "x");
            vm.HandleFileChange(new FileChangeEvent(ChangeType.Created, filePath));

            Assert.Contains(vm.RootNodes, n => n.Name == "new.txt");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// End-to-end: SetRootPath → real FileSystemWatcher → Buffer → ObserveOn(scheduler)
    /// → HandleFileChange → RootNodes. Uses CurrentThreadScheduler (prod uses AvaloniaScheduler).
    /// Waits via CollectionChanged + ManualResetEventSlim so the test thread does not
    /// need to pump a CurrentThreadScheduler trampoline for delivery (Buffer emits on
    /// DefaultScheduler; ObserveOn(CurrentThread) runs HandleFileChange on that thread).
    /// </summary>
    [Fact]
    public void SetRootPath_FullPipeline_WatcherCreated_UpdatesRootNodes()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-e2e-watcher-" + Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            using var service = new FileTreeService();
            var vm = new FileTreeViewModel(service, CurrentThreadScheduler.Instance);

            // Use trailing separator — the shape Avalonia folder pickers often return
            var pathWithSlash = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.True(vm.SetRootPath(pathWithSlash));
            Assert.Empty(vm.RootNodes);

            using var seen = new ManualResetEventSlim(false);
            void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                if (vm.RootNodes.Any(n => n.Name == "live-new.txt"))
                    seen.Set();
            }
            vm.RootNodes.CollectionChanged += OnCollectionChanged;

            // Settle period for the OS watcher (not for Rx delivery)
            Thread.Sleep(300);

            var filePath = Path.Combine(
                root.TrimEnd(Path.DirectorySeparatorChar),
                "live-new.txt");
            File.WriteAllText(filePath, "hello");

            Assert.True(
                seen.Wait(TimeSpan.FromSeconds(8)),
                "Expected RootNodes to update via SetRootPath → watcher → ObserveOn → HandleFileChange");
            Assert.Contains(vm.RootNodes, n => n.Name == "live-new.txt");

            vm.RootNodes.CollectionChanged -= OnCollectionChanged;
            vm.Dispose();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_DoesNotDelete_WhenNotConfirmed()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var filePath = Path.Combine(root, "keep-me.txt");
            File.WriteAllText(filePath, "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var node = vm.RootNodes.Single();
            vm.SelectedFile = node;

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(false));

            await vm.DeleteFileCommand.Execute(node);

            Assert.True(File.Exists(filePath));
            Assert.Single(vm.RootNodes);
            Assert.Equal(node, vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_RemovesFileFromTree_WhenConfirmed()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var filePath = Path.Combine(root, "remove-me.txt");
            File.WriteAllText(filePath, "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var node = vm.RootNodes.Single();

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(true));

            await vm.DeleteFileCommand.Execute(node);

            Assert.False(File.Exists(filePath));
            Assert.Empty(vm.RootNodes);
            Assert.Null(vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_DoesNotDeleteFolder_WhenNotConfirmed()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "keep-dir");
            Directory.CreateDirectory(dirPath);

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var dirNode = vm.RootNodes.Single();
            vm.SelectedFile = dirNode;

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(false));

            await vm.DeleteFileCommand.Execute(dirNode);

            Assert.True(Directory.Exists(dirPath));
            Assert.Single(vm.RootNodes);
            Assert.Equal(dirNode, vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_RemovesEmptyFolderFromTree_WhenConfirmed()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "empty-dir");
            Directory.CreateDirectory(dirPath);

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var dirNode = vm.RootNodes.Single();

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(true));

            await vm.DeleteFileCommand.Execute(dirNode);

            Assert.False(Directory.Exists(dirPath));
            Assert.Empty(vm.RootNodes);
            Assert.Null(vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_RemovesNonemptyFolderAndDescendantsFromTree_WhenConfirmed()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "parent-dir");
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, "child.txt"), "content");
            Directory.CreateDirectory(Path.Combine(dirPath, "sub"));
            File.WriteAllText(Path.Combine(dirPath, "sub", "grandchild.txt"), "nested");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var dirNode = vm.RootNodes.Single();
            Assert.Equal(2, dirNode.Children.Count);

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(true));

            await vm.DeleteFileCommand.Execute(dirNode);

            Assert.False(Directory.Exists(dirPath));
            Assert.Empty(vm.RootNodes);
            Assert.Null(vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_ClearsSelection_WhenChildInsideDeletedFolderWasSelected()
    {
        var vm = new FileTreeViewModel(_service, _scheduler);
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(root);
            var dirPath = Path.Combine(root, "parent-dir");
            Directory.CreateDirectory(dirPath);
            var childPath = Path.Combine(dirPath, "child.txt");
            File.WriteAllText(childPath, "content");

            vm.OpenFolderCommand.Execute(root).Subscribe(_ => { });
            var dirNode = vm.RootNodes.Single();
            var childNode = dirNode.Children.Single(c => !c.IsDirectory);
            vm.SelectedFile = childNode;

            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(true));

            await vm.DeleteFileCommand.Execute(dirNode);

            Assert.False(Directory.Exists(dirPath));
            Assert.Empty(vm.RootNodes);
            Assert.Null(vm.SelectedFile);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteFileCommand_SetsStatusText_AndRefreshes_OnFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "locked.txt");
        File.WriteAllText(filePath, "content");

        var mockService = new Moq.Mock<IFileTreeService>();
        mockService.Setup(s => s.EnumerateDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(() => new List<FileTreeNode>
            {
                new FileTreeNode
                {
                    Name = "locked.txt",
                    FullPath = filePath,
                    IsDirectory = false,
                    Depth = 0
                }
            });
        mockService.Setup(s => s.StartWatching(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Observable.Never<FileChangeEvent>());
        mockService.Setup(s => s.DeleteFile(filePath))
            .Throws(new UnauthorizedAccessException("access denied"));
        mockService.Setup(s => s.DeleteDirectory(It.IsAny<string>()));

        var vm = new FileTreeViewModel(mockService.Object, _scheduler);

        try
        {
            vm.SetRootPath(root);
            var node = vm.RootNodes.Single();
            vm.ConfirmDeleteFile.RegisterHandler(ctx => ctx.SetOutput(true));

            await vm.DeleteFileCommand.Execute(node);

            Assert.NotNull(vm.StatusText);
            Assert.Contains("Failed to delete file", vm.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("locked.txt", vm.StatusText, StringComparison.OrdinalIgnoreCase);
            mockService.Verify(s => s.EnumerateDirectory(root, It.IsAny<bool>()), Moq.Times.AtLeast(2));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

}
