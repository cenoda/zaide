using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class EditorTabViewModelTests
{
    private static EditorTabViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp);
    }

    [Fact]
    public void OpenFile_CreatesNewTab()
    {
        var vm = CreateViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");

        try
        {
            File.WriteAllText(filePath, "class Program { }");

            vm.OpenFileCommand.Execute(filePath).Subscribe(_ => { });

            Assert.Single(vm.OpenTabs);
            Assert.Equal(filePath, vm.OpenTabs[0].FilePath);
            Assert.Equal("class Program { }", vm.OpenTabs[0].TextContent);
            Assert.False(vm.OpenTabs[0].IsDirty,
                "File loaded via OpenFile should not be dirty");
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenFile_ActivatesExisting()
    {
        var vm = CreateViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".md");

        try
        {
            File.WriteAllText(filePath, "# Hello");

            // Open first time
            vm.OpenFileCommand.Execute(filePath).Subscribe(_ => { });
            var firstTab = vm.OpenTabs[0];

            // Open second time — should activate existing, not duplicate
            vm.OpenFileCommand.Execute(filePath).Subscribe(_ => { });

            Assert.Single(vm.OpenTabs);
            Assert.Same(firstTab, vm.ActiveTab);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CloseTab_RemovesFromCollection()
    {
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path1, "first");
            File.WriteAllText(path2, "second");

            vm.OpenFileCommand.Execute(path1).Subscribe(_ => { });
            vm.OpenFileCommand.Execute(path2).Subscribe(_ => { });

            Assert.Equal(2, vm.OpenTabs.Count);

            var tabToClose = vm.OpenTabs[0];
            vm.CloseTabCommand.Execute(tabToClose).Subscribe(_ => { });

            Assert.Single(vm.OpenTabs);
            Assert.DoesNotContain(tabToClose, vm.OpenTabs);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public void ActiveTab_DefaultsToNull()
    {
        var vm = CreateViewModel();
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public void CloseTab_StaysOpen_WhenSaveFails()
    {
        var vm = CreateViewModel();

        // Wire ConfirmClose to return true (user clicked Save)
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        // Open a tab pointing at a directory — save will fail
        var dir = Path.Combine(Path.GetTempPath(), "zaide-dir-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);

        try
        {
            var tab = new ServiceCollection()
                .AddTransient<EditorViewModel>()
                .BuildServiceProvider()
                .GetRequiredService<EditorViewModel>();

            tab.FilePath = dir; // directory → WriteAllText throws
            tab.TextContent = "unsaved";
            Assert.True(tab.IsDirty);

            vm.OpenTabs.Add(tab);
            vm.ActiveTab = tab;

            // Try closing — save should fail, tab must stay
            vm.CloseTabCommand.Execute(tab).Subscribe(_ => { });

            Assert.Single(vm.OpenTabs);
            Assert.True(tab.IsDirty);
            Assert.NotNull(vm.LastSaveError);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }

    [Fact]
    public void CloseTab_ActivatesNeighbor_WhenActiveClosed()
    {
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".json");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path1, "{}");
            File.WriteAllText(path2, "{}");

            vm.OpenFileCommand.Execute(path1).Subscribe(_ => { });
            vm.OpenFileCommand.Execute(path2).Subscribe(_ => { });

            // Tab 0 is active (last opened)
            Assert.Equal(2, vm.OpenTabs.Count);
            Assert.Same(vm.OpenTabs[1], vm.ActiveTab);

            // Close active tab → neighbor should become active
            vm.CloseTabCommand.Execute(vm.ActiveTab!).Subscribe(_ => { });

            Assert.Single(vm.OpenTabs);
            Assert.NotNull(vm.ActiveTab);
            Assert.Same(vm.OpenTabs[0], vm.ActiveTab);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }
}
