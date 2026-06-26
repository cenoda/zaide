using System;
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
}
