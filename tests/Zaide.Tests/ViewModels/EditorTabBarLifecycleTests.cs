using System;
using System.Collections.Specialized;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Tests for subscription cleanup and lifecycle patterns that EditorTabBar
/// depends on. EditorTabBar itself requires Avalonia runtime, so these tests
/// verify the observable-collection contract and rapid open/close behavior
/// at the ViewModel layer.
/// </summary>
public class EditorTabBarLifecycleTests
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
    public async Task CollectionChanged_Fires_OnAdd()
    {
        // EditorTabBar subscribes to CollectionChanged to add tab visuals.
        // Verify the collection fires Add events correctly.
        var vm = CreateViewModel();
        int addCount = 0;
        vm.OpenTabs.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                addCount++;
        };

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "content");
            await vm.OpenFileCommand.Execute(path);

            Assert.Equal(1, addCount);
            Assert.Single(vm.OpenTabs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task CollectionChanged_Fires_OnRemove()
    {
        // EditorTabBar subscribes to CollectionChanged to remove tab visuals.
        // Verify the collection fires Remove events correctly.
        var vm = CreateViewModel();
        int removeCount = 0;
        vm.OpenTabs.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
                removeCount++;
        };

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "content");
            await vm.OpenFileCommand.Execute(path);
            await vm.CloseTabCommand.Execute(vm.OpenTabs[0]);

            Assert.Equal(1, removeCount);
            Assert.Empty(vm.OpenTabs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task RapidOpenClose_DoesNotCorruptState()
    {
        // Rapidly open and close tabs to stress-test the collection
        // and subscription lifecycle. EditorTabBar's AddTab/RemoveTab
        // must keep up with fast collection changes.
        var vm = CreateViewModel();
        var paths = new string[5];

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = Path.Combine(Path.GetTempPath(),
                    "zaide-test-" + Guid.NewGuid() + ".txt");
                File.WriteAllText(paths[i], $"content {i}");
            }

            // Open all files
            foreach (var p in paths)
                await vm.OpenFileCommand.Execute(p);

            Assert.Equal(paths.Length, vm.OpenTabs.Count);

            // Close all tabs in reverse order
            for (int i = vm.OpenTabs.Count - 1; i >= 0; i--)
                await vm.CloseTabCommand.Execute(vm.OpenTabs[i]);

            Assert.Empty(vm.OpenTabs);
            Assert.Null(vm.ActiveTab);
        }
        finally
        {
            foreach (var p in paths)
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }
    }

    [Fact]
    public async Task OpenSameFileTwice_DoesNotDuplicateTab()
    {
        // EditorTabBar's AddTab should only be called once per unique path.
        // Opening the same file again activates the existing tab.
        var vm = CreateViewModel();
        int addCount = 0;
        vm.OpenTabs.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                addCount++;
        };

        var path = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path, "content");
            await vm.OpenFileCommand.Execute(path);
            await vm.OpenFileCommand.Execute(path); // duplicate

            Assert.Single(vm.OpenTabs);
            // CollectionChanged.Add should fire only once
            Assert.Equal(1, addCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task CloseAllTabs_LeavesCleanState()
    {
        // After closing all tabs, the collection must be empty and
        // ActiveTab null — no stale references that could cause
        // subscription leaks in EditorTabBar.
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path1, "a");
            File.WriteAllText(path2, "b");

            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);
            Assert.Equal(2, vm.OpenTabs.Count);

            await vm.CloseTabCommand.Execute(vm.OpenTabs[1]);
            await vm.CloseTabCommand.Execute(vm.OpenTabs[0]);

            Assert.Empty(vm.OpenTabs);
            Assert.Null(vm.ActiveTab);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public async Task CloseMiddleTab_PreservesOtherTabs()
    {
        // Closing a non-active, non-last tab must not disturb the
        // remaining tabs' order — EditorTabBar mirrors the collection.
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path3 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path1, "a");
            File.WriteAllText(path2, "b");
            File.WriteAllText(path3, "c");

            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);
            await vm.OpenFileCommand.Execute(path3);

            // Close the middle tab (index 1)
            var middleTab = vm.OpenTabs[1];
            await vm.CloseTabCommand.Execute(middleTab);

            Assert.Equal(2, vm.OpenTabs.Count);
            Assert.Equal(path1, vm.OpenTabs[0].FilePath);
            Assert.Equal(path3, vm.OpenTabs[1].FilePath);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
            if (File.Exists(path3)) File.Delete(path3);
        }
    }
}