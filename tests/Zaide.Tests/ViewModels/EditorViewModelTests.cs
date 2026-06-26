using System;
using System.IO;
using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class EditorViewModelTests
{
    [Fact]
    public void FileName_DerivedFromPath()
    {
        var vm = new EditorViewModel();

        vm.FilePath = "/home/user/project/Program.cs";

        Assert.Equal("Program.cs", vm.FileName);
    }

    [Fact]
    public void FileName_Untitled_WhenPathEmpty()
    {
        var vm = new EditorViewModel();

        vm.FilePath = "";

        Assert.Equal("Untitled", vm.FileName);
    }

    [Fact]
    public void IsDirty_DefaultsToFalse()
    {
        var vm = new EditorViewModel();

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void MarkDirty_WhenTextChanges()
    {
        var vm = new EditorViewModel();

        vm.TextContent = "hello world";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MarkClean_AfterSave()
    {
        var vm = new EditorViewModel();
        vm.TextContent = "hello";
        Assert.True(vm.IsDirty);

        vm.MarkClean();

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void SaveCommand_Fails_WhenPathIsDirectory()
    {
        var vm = new EditorViewModel();
        var dir = Path.Combine(Path.GetTempPath(), "zaide-dir-" + Guid.NewGuid());

        try
        {
            Directory.CreateDirectory(dir);
            vm.FilePath = dir; // directory, not a file → WriteAllText will fail
            vm.TextContent = "should not save";
            Assert.True(vm.IsDirty);

            var result = true;
            vm.SaveCommand.Execute().Subscribe(r => result = r);

            Assert.False(result, "Save should return false on I/O failure");
            Assert.True(vm.IsDirty, "Dirty flag should remain true after failed save");
            Assert.NotNull(vm.LastSaveError);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }

    [Fact]
    public void SaveCommand_WritesFile()
    {
        var vm = new EditorViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(filePath, "original");
            vm.FilePath = filePath;
            vm.TextContent = "hello world";

            var result = false;
            vm.SaveCommand.Execute().Subscribe(r => result = r);

            Assert.True(result);
            var saved = File.ReadAllText(filePath);
            Assert.Equal("hello world", saved);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void SaveCommand_ClearsDirty()
    {
        var vm = new EditorViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(filePath, "original");
            vm.FilePath = filePath;
            vm.TextContent = "modified";
            Assert.True(vm.IsDirty);

            vm.SaveCommand.Execute().Subscribe();

            Assert.False(vm.IsDirty);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
