using System;
using System.IO;
using System.Reactive;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class FileTreeViewModelTests
{
    private readonly FileTreeService _service = new();

    [Fact]
    public void RootNodes_IsEmpty_BeforeFolderOpened()
    {
        var vm = new FileTreeViewModel(_service);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void OpenFolderCommand_PopulatesRootNodes_ForTempDirectory()
    {
        var vm = new FileTreeViewModel(_service);
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
        var vm = new FileTreeViewModel(_service);
        Assert.Null(vm.SelectedFile);
    }
}
