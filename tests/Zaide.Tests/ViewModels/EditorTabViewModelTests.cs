using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Tests.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.ViewModels;

public class EditorTabViewModelTests
{
    private static EditorTabViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
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
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs, sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());

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
            var tab = new EditorViewModel(new Document(""), new FileService());
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
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs, sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());

        // ConfirmClose returns true (user says "Save")
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var tab = new EditorViewModel(new Document(""), mockFs);
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
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs, sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());

        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var tab = new EditorViewModel(new Document(""), mockFs);
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

    // ── Phase 11 F9: SaveAllDirtyTabsAsync ───────────────────────────

    [Fact]
    public async Task SaveAllDirtyTabs_NoDirtyTabs_ReturnsTrue()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");

        try
        {
            File.WriteAllText(path, "class Program { }");
            await vm.OpenFileCommand.Execute(path);

            Assert.False(vm.OpenTabs[0].IsDirty);

            var result = await vm.SaveAllDirtyTabsAsync();

            Assert.True(result);
            Assert.Null(vm.LastSaveError);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAllDirtyTabs_SavesAllDirtyTabs()
    {
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");

        try
        {
            File.WriteAllText(path1, "class A { }");
            File.WriteAllText(path2, "class B { }");
            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);

            // Make both tabs dirty
            vm.OpenTabs[0].TextContent = "class A { int x; }";
            vm.OpenTabs[1].TextContent = "class B { int y; }";
            Assert.True(vm.OpenTabs[0].IsDirty);
            Assert.True(vm.OpenTabs[1].IsDirty);

            var result = await vm.SaveAllDirtyTabsAsync();

            Assert.True(result);
            Assert.False(vm.OpenTabs[0].IsDirty);
            Assert.False(vm.OpenTabs[1].IsDirty);
            Assert.Null(vm.LastSaveError);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public async Task SaveAllDirtyTabs_SaveFailure_StopsAndReturnsFalse()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs, sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());

        var tab = new EditorViewModel(new Document(""), mockFs);
        tab.FilePath = "/tmp/save-fail.txt";
        tab.TextContent = "dirty";
        Assert.True(tab.IsDirty);
        vm.OpenTabs.Add(tab);

        var result = await vm.SaveAllDirtyTabsAsync();

        Assert.False(result);
        Assert.True(tab.IsDirty, "Dirty flag should remain after failed save");
        Assert.NotNull(vm.LastSaveError);
        Assert.Equal("disk full", vm.LastSaveError);
    }

    [Fact]
    public async Task SaveAllDirtyTabs_SkipsCleanTabs()
    {
        var vm = CreateViewModel();
        var pathClean = Path.Combine(Path.GetTempPath(), "zaide-clean-" + Guid.NewGuid() + ".cs");
        var pathDirty = Path.Combine(Path.GetTempPath(), "zaide-dirty-" + Guid.NewGuid() + ".cs");

        try
        {
            File.WriteAllText(pathClean, "class Clean { }");
            File.WriteAllText(pathDirty, "class Dirty { }");
            await vm.OpenFileCommand.Execute(pathClean);
            await vm.OpenFileCommand.Execute(pathDirty);

            // Make only the second tab dirty
            vm.OpenTabs[1].TextContent = "class Dirty { int x; }";
            Assert.False(vm.OpenTabs[0].IsDirty, "First tab should be clean");
            Assert.True(vm.OpenTabs[1].IsDirty, "Second tab should be dirty");

            var result = await vm.SaveAllDirtyTabsAsync();

            Assert.True(result);
            Assert.False(vm.OpenTabs[0].IsDirty, "Clean tab should stay clean");
            Assert.False(vm.OpenTabs[1].IsDirty, "Dirty tab should be saved");
            Assert.Null(vm.LastSaveError);

            // Verify clean tab content was not rewritten
            var onDisk = File.ReadAllText(pathClean);
            Assert.Equal("class Clean { }", onDisk);
        }
        finally
        {
            if (File.Exists(pathClean)) File.Delete(pathClean);
            if (File.Exists(pathDirty)) File.Delete(pathDirty);
        }
    }

    [Fact]
    public async Task SaveAllDirtyTabs_MultipleDirty_StopsOnFirstFailure()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp, mockFs, sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());

        var tab1 = new EditorViewModel(new Document(""), mockFs);
        tab1.FilePath = "/tmp/save-1.txt";
        tab1.TextContent = "dirty 1";

        var tab2 = new EditorViewModel(new Document(""), mockFs);
        tab2.FilePath = "/tmp/save-2.txt";
        tab2.TextContent = "dirty 2";

        vm.OpenTabs.Add(tab1);
        vm.OpenTabs.Add(tab2);
        Assert.True(tab1.IsDirty);
        Assert.True(tab2.IsDirty);

        var result = await vm.SaveAllDirtyTabsAsync();

        Assert.False(result);
        Assert.True(tab1.IsDirty, "First tab save failed — should stay dirty");
        Assert.True(tab2.IsDirty, "Second tab should remain dirty (never reached)");
        Assert.NotNull(vm.LastSaveError);
    }
}
