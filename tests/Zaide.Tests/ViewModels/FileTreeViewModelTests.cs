using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class FileTreeViewModelTests
{
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
}
