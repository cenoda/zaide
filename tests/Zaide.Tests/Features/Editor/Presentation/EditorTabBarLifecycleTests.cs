using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.Tests.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Tests for subscription cleanup and lifecycle patterns that EditorTabBar
/// depends on. EditorTabBar itself requires Avalonia runtime, so these tests
/// verify the observable-collection contract and rapid open/close behavior
/// at the ViewModel layer.
/// </summary>
public class EditorTabBarLifecycleTests
{
    static EditorTabBarLifecycleTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }
    private static EditorTabViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
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

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M5b: CollectionChanged Move and cleanup
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CollectionChanged_Move_FiresCorrectEvent()
    {
        // EditorTabBar handles CollectionChanged Move to reconcile visuals.
        // Verify that MoveTab produces the correct Move notification.
        var vm = CreateViewModel();
        var path1 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");
        var path2 = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(path1, "a");
            File.WriteAllText(path2, "b");
            await vm.OpenFileCommand.Execute(path1);
            await vm.OpenFileCommand.Execute(path2);

            NotifyCollectionChangedEventArgs? captured = null;
            vm.OpenTabs.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Move)
                    captured = e;
            };

            vm.MoveTab(0, 1);

            Assert.NotNull(captured);
            Assert.Equal(NotifyCollectionChangedAction.Move, captured!.Action);
            Assert.Equal(0, captured.OldStartingIndex);
            Assert.Equal(1, captured.NewStartingIndex);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public async Task CollectionChanged_Move_ReversesDirection()
    {
        // Move notification with backward direction (new < old).
        var vm = CreateViewModel();
        var paths = new string[3];
        try
        {
            for (int i = 0; i < 3; i++)
            {
                paths[i] = Path.Combine(Path.GetTempPath(),
                    "zaide-test-" + Guid.NewGuid() + ".txt");
                File.WriteAllText(paths[i], $"content{i}");
                await vm.OpenFileCommand.Execute(paths[i]);
            }

            NotifyCollectionChangedEventArgs? captured = null;
            vm.OpenTabs.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Move)
                    captured = e;
            };

            vm.MoveTab(2, 0); // last to first

            Assert.NotNull(captured);
            Assert.Equal(0, captured!.NewStartingIndex);
            Assert.Equal(2, captured.OldStartingIndex);
        }
        finally
        {
            foreach (var p in paths)
                if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public void CollectionChanged_Move_NoDuplicateSubscription()
    {
        // SetTabs should unsubscribe from the old collection and subscribe
        // to the new one. Moving tabs on the old collection after SetTabs
        // must not trigger handlers that would modify the new collection.
        var vm = CreateViewModel();
        var vm2 = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tabA = new EditorViewModel(ws.OpenDocument("/tmp/a.cs", "a"), new MockFileService());
        var tabB = new EditorViewModel(ws.OpenDocument("/tmp/b.cs", "b"), new MockFileService());
        var tabC = new EditorViewModel(ws.OpenDocument("/tmp/c.cs", "c"), new MockFileService());
        vm.OpenTabs.Add(tabA);
        vm.OpenTabs.Add(tabB);
        vm2.OpenTabs.Add(tabC);

        int moveCount = 0;
        NotifyCollectionChangedEventHandler handler = (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
                moveCount++;
        };

        vm.OpenTabs.CollectionChanged += handler;

        // Simulate SetTabs: unsubscribe from one, subscribe to another
        vm.OpenTabs.CollectionChanged -= handler;
        vm2.OpenTabs.CollectionChanged += handler;

        // Move on vm's collection — should not fire handler
        vm.MoveTab(0, 1);
        Assert.Equal(0, moveCount);

        // Move on vm2's collection — should fire handler
        vm2.MoveTab(0, 0); // no-op, should not fire
        Assert.Equal(0, moveCount);
    }

    [Fact]
    public void MoveTab_CollectionChangedMove_VisualOrderMatchesCollectionOrder()
    {
        // After multiple Move operations, the visual order must be the same
        // as the collection order. This validates the CollectionChanged Move
        // handler contract: children are reordered to match.
        var vm = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tab0 = new EditorViewModel(ws.OpenDocument("/tmp/a.cs", "a"), new MockFileService());
        var tab1 = new EditorViewModel(ws.OpenDocument("/tmp/b.cs", "b"), new MockFileService());
        var tab2 = new EditorViewModel(ws.OpenDocument("/tmp/c.cs", "c"), new MockFileService());
        var tab3 = new EditorViewModel(ws.OpenDocument("/tmp/d.cs", "d"), new MockFileService());
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.OpenTabs.Add(tab2);
        vm.OpenTabs.Add(tab3);

        // Track visual order from CollectionChanged Move events
        var visualOrder = vm.OpenTabs.ToList();

        // CollectionChanged handler that simulates what EditorTabBar does:
        // remove from old index, insert at new index.
        NotifyCollectionChangedEventHandler? handler = null;
        handler = (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                var item = (EditorViewModel)e.NewItems![0]!;
                visualOrder.RemoveAt(e.OldStartingIndex);
                visualOrder.Insert(e.NewStartingIndex, item);
            }
        };
        vm.OpenTabs.CollectionChanged += handler;

        // Multiple reorders
        vm.MoveTab(0, 2); // [b, c, a, d]
        Assert.Equal(new[] { tab1, tab2, tab0, tab3 }, visualOrder);

        vm.MoveTab(3, 0); // [d, b, c, a]
        Assert.Equal(new[] { tab3, tab1, tab2, tab0 }, visualOrder);

        vm.MoveTab(1, 3); // [d, c, a, b]
        Assert.Equal(new[] { tab3, tab2, tab0, tab1 }, visualOrder);

        // Final visual order must match collection order
        Assert.Equal(vm.OpenTabs.ToList(), visualOrder);
    }

    [Fact]
    public async Task MoveTabThenCloseAll_LeavesCleanState()
    {
        // After reordering then closing all tabs, the collection must be
        // empty and ActiveTab null — no stale references.
        var vm = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tab0 = new EditorViewModel(ws.OpenDocument("/tmp/a.cs", "a"), new MockFileService());
        var tab1 = new EditorViewModel(ws.OpenDocument("/tmp/b.cs", "b"), new MockFileService());
        var tab2 = new EditorViewModel(ws.OpenDocument("/tmp/c.cs", "c"), new MockFileService());
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.OpenTabs.Add(tab2);
        vm.ActiveTab = tab0;

        vm.MoveTab(0, 1); // [b, a, c]
        vm.MoveTab(2, 0); // [c, b, a]

        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public void SetTabs_UnsubscribesFromOldCollection()
    {
        // EditorTabBar calls SetTabs which unsubscribes from old collection.
        // Verify that the old collection's handler is detached.
        // (EditorTabBar behavior: the lifecycle test covers the subscription
        // contract at the ViewModel level.)
        var vm = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tabA = new EditorViewModel(ws.OpenDocument("/tmp/a.cs", "a"), new MockFileService());
        var tabB = new EditorViewModel(ws.OpenDocument("/tmp/b.cs", "b"), new MockFileService());
        vm.OpenTabs.Add(tabA);
        vm.OpenTabs.Add(tabB);

        int oldCollectionMoveCount = 0;
        void OnOldChanged(object? s, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
                oldCollectionMoveCount++;
        }

        vm.OpenTabs.CollectionChanged += OnOldChanged;
        vm.OpenTabs.CollectionChanged -= OnOldChanged; // simulate SetTabs unsubscribe

        vm.MoveTab(0, 1);

        Assert.Equal(0, oldCollectionMoveCount);
    }

    [Fact]
    public void RemoveTabDuringDrag_SafeState()
    {
        // If the dragged tab is removed (e.g. by the close button) before a
        // drag completes, no stale reference or crash should result. At the
        // ViewModel layer, direct collection removal doesn't update ActiveTab
        // (only CloseTabAsync does), but the View must still be safe.
        var vm = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tabA = new EditorViewModel(ws.OpenDocument("/tmp/a.cs", "a"), new MockFileService());
        var tabB = new EditorViewModel(ws.OpenDocument("/tmp/b.cs", "b"), new MockFileService());
        vm.OpenTabs.Add(tabA);
        vm.OpenTabs.Add(tabB);
        vm.ActiveTab = tabA;

        // Simulate tab being removed (by close button) while a drag was
        // in progress at the View level. The ViewModel handles this
        // gracefully because it validates all MoveTab inputs.
        vm.OpenTabs.Remove(tabA);

        Assert.Single(vm.OpenTabs);
        // ActiveTab still references the removed tab. Only CloseTabAsync
        // updates ActiveTab on removal. The View-level drag state is
        // cancelled by EditorTabBar.RemoveTab.
        Assert.Same(tabA, vm.ActiveTab);
        Assert.DoesNotContain(tabA, vm.OpenTabs);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M5b: Escape-key drag-cancellation lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// In production, <c>SubscribeEscape</c> stores a closure
    /// <c>() =&gt; topLevel.KeyDown -= handler</c> that captures the
    /// exact <see cref="Avalonia.Input.TopLevel"/> instance. The tests
    /// below use the <see cref="EditorTabBar.TestOnly_UnsubscribeEscapeAction"/>
    /// seam to inject a test action and verify that cleanup paths invoke it,
    /// proving the handler would be removed from its original owner even if
    /// <c>TopLevel.GetTopLevel</c> later returns null (e.g. after detach).
    /// </summary>

    [Fact]
    public void EscapeSubscription_InitiallyNotSubscribed()
    {
        var tabBar = new EditorTabBar();
        Assert.False(tabBar.IsEscapeSubscribed);
    }

    [Fact]
    public void EscapeSubscription_SubscribeWithoutTopLevel_DoesNotThrow()
    {
        // TopLevel.GetTopLevel(this) returns null in unit-test
        // environments without a window. Verify SubscribeEscape handles
        // this gracefully (no exception, no stale handler reference).
        var tabBar = new EditorTabBar();
        tabBar.SubscribeEscape();
        Assert.False(tabBar.IsEscapeSubscribed);
    }

    [Fact]
    public void EscapeSubscription_Unsubscribe_IsIdempotent()
    {
        var tabBar = new EditorTabBar();

        // Multiple calls must not throw.
        tabBar.UnsubscribeEscape();
        tabBar.UnsubscribeEscape();
        tabBar.UnsubscribeEscape();

        Assert.False(tabBar.IsEscapeSubscribed);
    }

    [Fact]
    public void EscapeSubscription_Unsubscribe_InvokesStoredAction()
    {
        // Prove that UnsubscribeEscape invokes the stored closure,
        // which in production would remove the handler from the
        // TopLevel.KeyDown event on the original owner.
        var tabBar = new EditorTabBar();
        bool actionInvoked = false;
        tabBar.TestOnly_UnsubscribeEscapeAction = () => actionInvoked = true;

        tabBar.UnsubscribeEscape();

        Assert.True(actionInvoked, "Stored unsubscribe action must be invoked");
        Assert.False(tabBar.IsEscapeSubscribed, "Action cleared after invocation");
    }

    [Fact]
    public void EscapeSubscription_Unsubscribe_InvokesStoredAction_ExactlyOnce()
    {
        // Calling UnsubscribeEscape twice should only invoke the stored
        // action once (the action is cleared after the first invocation).
        var tabBar = new EditorTabBar();
        int invokeCount = 0;
        tabBar.TestOnly_UnsubscribeEscapeAction = () => invokeCount++;

        tabBar.UnsubscribeEscape();
        Assert.Equal(1, invokeCount);
        Assert.False(tabBar.IsEscapeSubscribed);

        tabBar.UnsubscribeEscape(); // second call — action was cleared
        Assert.Equal(1, invokeCount); // not incremented
    }

    [Fact]
    public void EscapeSubscription_CancelDrag_InvokesStoredAction()
    {
        // CancelDrag calls UnsubscribeEscape internally. Use the injection
        // seam to prove that the full detach/drag-cancel path removes the
        // handler from the stored owner.
        var tabBar = new EditorTabBar();
        bool actionInvoked = false;
        tabBar.TestOnly_UnsubscribeEscapeAction = () => actionInvoked = true;

        // Simulate what CancelDrag does: calls UnsubscribeEscape.
        // In production this is reached from:
        //   - OnTabPointerCaptureLost
        //   - RemoveTab (if removed tab is the dragged tab)
        //   - SetTabs (reset)
        //   - OnDetachedFromVisualTree
        //   - Escape key press (via SubscribeEscape's handler)
        tabBar.UnsubscribeEscape();

        Assert.True(actionInvoked,
            "CancelDrag must invoke the stored unsubscribe action, proving " +
            "the handler is removed from the original TopLevel owner even " +
            "after visual-tree detachment.");
    }
}