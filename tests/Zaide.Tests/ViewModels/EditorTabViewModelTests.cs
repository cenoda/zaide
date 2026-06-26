using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.Services;
using Zaide.Tests.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class EditorTabViewModelTests
{
    private static EditorTabViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>());
    }

    [Fact]
    public async Task OpenFile_CreatesNewTab()
    {
        var vm = CreateViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");

        try
        {
            File.WriteAllText(filePath, "class Program { }");

            await vm.OpenFileCommand.Execute(filePath);

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
    public async Task OpenFile_ActivatesExisting()
    {
        var vm = CreateViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".md");

        try
        {
            File.WriteAllText(filePath, "# Hello");

            // Open first time
            await vm.OpenFileCommand.Execute(filePath);
            var firstTab = vm.OpenTabs[0];

            // Open second time — should activate existing, not duplicate
            await vm.OpenFileCommand.Execute(filePath);

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
    public async Task OpenFile_CaseVariant_ActivatesExisting_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var mockFs = new MockFileService();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs);

        await vm.OpenFileCommand.Execute(@"C:\Temp\Readme.md");
        var firstTab = vm.OpenTabs[0];

        await vm.OpenFileCommand.Execute(@"c:\temp\README.md");

        Assert.Single(vm.OpenTabs);
        Assert.Same(firstTab, vm.ActiveTab);
    }

    [Fact]
    public async Task CloseTab_RemovesFromCollection()
    {
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path1, "first");
            File.WriteAllText(path2, "second");

            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);

            Assert.Equal(2, vm.OpenTabs.Count);

            var tabToClose = vm.OpenTabs[0];
            await vm.CloseTabCommand.Execute(tabToClose);

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
    public async Task CloseTab_StaysOpen_WhenSaveFails()
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
                .AddSingleton<IFileService, FileService>()
                .AddTransient<EditorViewModel>()
                .BuildServiceProvider()
                .GetRequiredService<EditorViewModel>();

            tab.FilePath = dir; // directory → WriteAllText throws
            tab.TextContent = "unsaved";
            Assert.True(tab.IsDirty);

            vm.OpenTabs.Add(tab);
            vm.ActiveTab = tab;

            // Try closing — save should fail, tab must stay
            await vm.CloseTabCommand.Execute(tab);

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
    public async Task CloseTab_ActivatesNeighbor_WhenActiveClosed()
    {
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".json");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path1, "{}");
            File.WriteAllText(path2, "{}");

            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);

            // Tab 0 is active (last opened)
            Assert.Equal(2, vm.OpenTabs.Count);
            Assert.Same(vm.OpenTabs[1], vm.ActiveTab);

            // Close active tab → neighbor should become active
            await vm.CloseTabCommand.Execute(vm.ActiveTab!);

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

    // ---------------------------------------------------------------
    // Risky-path tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task SaveFailure_MustNotCloseTab_MockFileService()
    {
        // Uses MockFileService so the failure is deterministic and does not
        // depend on OS permissions or filesystem quirks.
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs);

        // ConfirmClose returns true (user says "Save")
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var tab = sp.GetRequiredService<EditorViewModel>();
        tab.FilePath = "/tmp/fake-save-fail.txt";
        tab.TextContent = "dirty content";
        Assert.True(tab.IsDirty);

        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;

        // Attempt close → save fails → tab must remain
        await vm.CloseTabCommand.Execute(tab);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tab, vm.OpenTabs[0]);
        Assert.True(tab.IsDirty);
        Assert.NotNull(vm.LastSaveError);
        Assert.Equal("disk full", vm.LastSaveError);
    }

    [Fact]
    public async Task CancelClose_OnDirtyTab_DoesNotRemoveTab()
    {
        // ConfirmClose returns null → user cancels the dialog.
        var vm = CreateViewModel();
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(null));

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "original");
            await vm.OpenFileCommand.Execute(path);

            var tab = vm.OpenTabs[0];
            tab.TextContent = "modified"; // mark dirty
            Assert.True(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            // Tab must NOT be removed — user cancelled
            Assert.Single(vm.OpenTabs);
            Assert.Same(tab, vm.OpenTabs[0]);
            Assert.True(tab.IsDirty, "Tab should still be dirty after cancel");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task CloseWithoutSave_OnDirtyTab_RemovesTab()
    {
        // ConfirmClose returns false → user chose "Close without saving".
        var vm = CreateViewModel();
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(false));

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "original");
            await vm.OpenFileCommand.Execute(path);

            var tab = vm.OpenTabs[0];
            tab.TextContent = "modified"; // mark dirty
            Assert.True(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            // Tab removed without saving
            Assert.Empty(vm.OpenTabs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveFailure_SetsLastErrorOnTab()
    {
        // Verify LastSaveError is propagated from the tab to the tab manager.
        var mockFs = new MockFileService
        {
            WriteException = new UnauthorizedAccessException("permission denied")
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs);

        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var tab = sp.GetRequiredService<EditorViewModel>();
        tab.FilePath = "/tmp/fake-perm.txt";
        tab.TextContent = "dirty";
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;

        await vm.CloseTabCommand.Execute(tab);

        Assert.Single(vm.OpenTabs);
        Assert.NotNull(vm.LastSaveError);
        Assert.Contains("permission denied", vm.LastSaveError);
    }

    [Fact]
    public async Task ConfirmClose_NotRaised_WhenTabIsClean()
    {
        // If the tab is clean, the close should go through without
        // ever touching the ConfirmClose interaction.
        var vm = CreateViewModel();
        bool handlerCalled = false;
        vm.ConfirmClose.RegisterHandler(ctx =>
        {
            handlerCalled = true;
            ctx.SetOutput(true);
        });

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "content");
            await vm.OpenFileCommand.Execute(path);

            var tab = vm.OpenTabs[0];
            Assert.False(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            Assert.Empty(vm.OpenTabs);
            Assert.False(handlerCalled, "ConfirmClose should not be raised for a clean tab");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
