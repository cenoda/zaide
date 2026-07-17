using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 9 M5b: Tests for pointer-driven tab reordering via
/// <see cref="EditorTabViewModel.MoveTab"/>.
/// </summary>
public sealed class EditorTabReorderTests
{
    static EditorTabReorderTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static EditorTabViewModel CreateViewModel()
    {
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var mockFs = new MockFileService();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp, mockFs, ws);
    }

    /// <summary>
    /// Creates a tab, registers its document in <paramref name="ws"/>,
    /// and adds it to <paramref name="vm"/>.
    /// </summary>
    private static EditorViewModel AddTab(global::Zaide.Features.Workspace.Domain.Workspace ws, EditorTabViewModel vm, string path)
    {
        var doc = ws.OpenDocument(path, $"{path} content");
        var tab = new EditorViewModel(doc, new MockFileService());
        vm.OpenTabs.Add(tab);
        return tab;
    }

    /// <summary>
    /// Creates N tabs with deterministic paths.
    /// </summary>
    private static EditorViewModel[] AddTabs(EditorTabViewModel vm, int count)
    {
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var tabs = new EditorViewModel[count];
        for (int i = 0; i < count; i++)
        {
            tabs[i] = new EditorViewModel(
                ws.OpenDocument($"/tmp/tab{i}.cs", $"content{i}"),
                new MockFileService());
            vm.OpenTabs.Add(tabs[i]);
        }

        return tabs;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Validation — invalid inputs produce no change
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_NegativeFromIndex_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 3);
        var order = vm.OpenTabs.Select(t => t.FilePath).ToList();

        vm.MoveTab(-1, 1);

        Assert.Equal(order, vm.OpenTabs.Select(t => t.FilePath));
    }

    [Fact]
    public void MoveTab_FromIndexTooHigh_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 3);
        var order = vm.OpenTabs.Select(t => t.FilePath).ToList();

        vm.MoveTab(5, 1);

        Assert.Equal(order, vm.OpenTabs.Select(t => t.FilePath));
    }

    [Fact]
    public void MoveTab_NegativeToIndex_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 3);
        var order = vm.OpenTabs.Select(t => t.FilePath).ToList();

        vm.MoveTab(1, -1);

        Assert.Equal(order, vm.OpenTabs.Select(t => t.FilePath));
    }

    [Fact]
    public void MoveTab_ToIndexTooHigh_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 3);
        var order = vm.OpenTabs.Select(t => t.FilePath).ToList();

        vm.MoveTab(1, 5);

        Assert.Equal(order, vm.OpenTabs.Select(t => t.FilePath));
    }

    [Fact]
    public void MoveTab_SameIndex_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 3);
        var order = vm.OpenTabs.Select(t => t.FilePath).ToList();

        vm.MoveTab(1, 1);
        vm.MoveTab(0, 0);
        vm.MoveTab(2, 2);

        Assert.Equal(order, vm.OpenTabs.Select(t => t.FilePath));
    }

    [Fact]
    public void MoveTab_EmptyCollection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.MoveTab(0, 1); // neither should throw
        Assert.Empty(vm.OpenTabs);
    }

    [Fact]
    public void MoveTab_SingleTab_DoesNothing()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 1);

        vm.MoveTab(0, 0); // same index, no-op
        Assert.Single(vm.OpenTabs);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Move operations — ordering correctness
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_FromFirstToLast_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);

        vm.MoveTab(0, 2);

        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.Same(tabs[1], vm.OpenTabs[0]);
        Assert.Same(tabs[2], vm.OpenTabs[1]);
        Assert.Same(tabs[0], vm.OpenTabs[2]);
    }

    [Fact]
    public void MoveTab_FromLastToFirst_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);

        vm.MoveTab(2, 0);

        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.Same(tabs[2], vm.OpenTabs[0]);
        Assert.Same(tabs[0], vm.OpenTabs[1]);
        Assert.Same(tabs[1], vm.OpenTabs[2]);
    }

    [Fact]
    public void MoveTab_FromMiddleToFirst_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);

        vm.MoveTab(1, 0);

        Assert.Equal(4, vm.OpenTabs.Count);
        Assert.Same(tabs[1], vm.OpenTabs[0]);
        Assert.Same(tabs[0], vm.OpenTabs[1]);
        Assert.Same(tabs[2], vm.OpenTabs[2]);
        Assert.Same(tabs[3], vm.OpenTabs[3]);
    }

    [Fact]
    public void MoveTab_FromMiddleToLast_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);

        vm.MoveTab(2, 3);

        Assert.Equal(4, vm.OpenTabs.Count);
        Assert.Same(tabs[0], vm.OpenTabs[0]);
        Assert.Same(tabs[1], vm.OpenTabs[1]);
        Assert.Same(tabs[3], vm.OpenTabs[2]);
        Assert.Same(tabs[2], vm.OpenTabs[3]);
    }

    [Fact]
    public void MoveTab_ForwardByOne_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);

        vm.MoveTab(1, 2);

        Assert.Equal(4, vm.OpenTabs.Count);
        Assert.Same(tabs[0], vm.OpenTabs[0]);
        Assert.Same(tabs[2], vm.OpenTabs[1]);
        Assert.Same(tabs[1], vm.OpenTabs[2]);
        Assert.Same(tabs[3], vm.OpenTabs[3]);
    }

    [Fact]
    public void MoveTab_BackwardByOne_UpdatesOrder()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);

        vm.MoveTab(2, 1);

        Assert.Equal(4, vm.OpenTabs.Count);
        Assert.Same(tabs[0], vm.OpenTabs[0]);
        Assert.Same(tabs[2], vm.OpenTabs[1]);
        Assert.Same(tabs[1], vm.OpenTabs[2]);
        Assert.Same(tabs[3], vm.OpenTabs[3]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CollectionChanged notification
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_FiresCollectionChangedMove()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 4);
        NotifyCollectionChangedEventArgs? captured = null;
        vm.OpenTabs.CollectionChanged += (_, e) => captured = e;

        vm.MoveTab(0, 2);

        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Move, captured!.Action);
        Assert.Single(captured.NewItems!);
        Assert.Equal(2, captured.NewStartingIndex);
        Assert.Equal(0, captured.OldStartingIndex);
    }

    [Fact]
    public void MoveTab_FiresCollectionChangedMove_ReverseDirection()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 4);
        NotifyCollectionChangedEventArgs? captured = null;
        vm.OpenTabs.CollectionChanged += (_, e) => captured = e;

        vm.MoveTab(3, 0);

        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Move, captured!.Action);
        Assert.Equal(0, captured.NewStartingIndex);
        Assert.Equal(3, captured.OldStartingIndex);
    }

    [Fact]
    public void MoveTab_NoOp_DoesNotFireCollectionChanged()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 4);
        int fireCount = 0;
        vm.OpenTabs.CollectionChanged += (_, _) => fireCount++;

        vm.MoveTab(1, 1); // same index — no-op

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void MoveTab_InvalidIndex_DoesNotFireCollectionChanged()
    {
        var vm = CreateViewModel();
        AddTabs(vm, 4);
        int fireCount = 0;
        vm.OpenTabs.CollectionChanged += (_, _) => fireCount++;

        vm.MoveTab(-1, 2);
        vm.MoveTab(4, 2);
        vm.MoveTab(1, -1);
        vm.MoveTab(1, 4);

        Assert.Equal(0, fireCount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Active-tab preservation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_ActiveTabReferencePreserved()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);
        vm.ActiveTab = tabs[2];

        vm.MoveTab(0, 2);

        Assert.Same(tabs[2], vm.ActiveTab);
    }

    [Fact]
    public void MoveTab_ActiveTabStillAtCorrectIndex_AfterMoveOthers()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);
        vm.ActiveTab = tabs[2]; // third tab, stays at index 2

        // Move first tab to last — active tab at 2 is unaffected
        vm.MoveTab(0, 3);

        Assert.Same(tabs[2], vm.ActiveTab);
        // tabs[2] should now be at index 1 (since tab[0] moved from 0 to 3,
        // indices 1,2,3 shifted to 0,1,2)
        // Before move: [0=tab0, 1=tab1, 2=active, 3=tab3]
        // Move(0, 3): remove [0] → [tab1(0), active(1), tab3(2)], insert at 3 → [tab1, active, tab3, tab0]
        Assert.Same(tabs[2], vm.OpenTabs[1]);
    }

    [Fact]
    public void MoveTab_ActiveTabAtFront_StaysAtFrontAfterMove()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        vm.ActiveTab = tabs[0];

        vm.MoveTab(2, 1); // move last to middle — should not affect active at 0

        Assert.Same(tabs[0], vm.ActiveTab);
        Assert.Same(tabs[0], vm.OpenTabs[0]);
    }

    [Fact]
    public void MoveTab_ActiveTabMoves_StaysActive()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        vm.ActiveTab = tabs[0];

        vm.MoveTab(0, 2); // move active from first to last

        Assert.Same(tabs[0], vm.ActiveTab);
        Assert.Same(tabs[0], vm.OpenTabs[2]);
    }

    [Fact]
    public void MoveTab_WorkspaceActiveDocument_Unchanged()
    {
        var vm = CreateViewModel();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var mockFs = new MockFileService();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        vm = new EditorTabViewModel(sp, mockFs, ws);

        var docA = ws.OpenDocument("/tmp/a.cs", "a");
        var tabA = new EditorViewModel(docA, mockFs);
        vm.OpenTabs.Add(tabA);
        var docB = ws.OpenDocument("/tmp/b.cs", "b");
        var tabB = new EditorViewModel(docB, mockFs);
        vm.OpenTabs.Add(tabB);
        var docC = ws.OpenDocument("/tmp/c.cs", "c");
        var tabC = new EditorViewModel(docC, mockFs);
        vm.OpenTabs.Add(tabC);
        vm.ActiveTab = tabB;

        vm.MoveTab(0, 2);

        Assert.Same(tabB.Document, ws.ActiveDocument);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dirty-state and display-name preservation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_DirtyTab_StatePreserved()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        tabs[1].TextContent = "modified";
        Assert.True(tabs[1].IsDirty);

        vm.MoveTab(0, 2);

        Assert.True(tabs[1].IsDirty);
        Assert.Equal("● tab1.cs", tabs[1].DisplayName);
    }

    [Fact]
    public void MoveTab_AllTabs_ContentAndDirtyStatePreserved()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);
        tabs[0].TextContent = "modified 0";
        tabs[2].TextContent = "modified 2";
        var origContent = tabs.Select(t => t.TextContent).ToArray();

        vm.MoveTab(0, 2);
        vm.MoveTab(3, 0);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(origContent[i], tabs[i].TextContent);
        }

        Assert.True(tabs[0].IsDirty);
        Assert.False(tabs[1].IsDirty);
        Assert.True(tabs[2].IsDirty);
        Assert.False(tabs[3].IsDirty);
    }

    [Fact]
    public void MoveTab_FileNamesAndDisplayNamesPreserved()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        var names = tabs.Select(t => t.DisplayName).ToArray();

        vm.MoveTab(0, 2);

        for (int i = 0; i < 3; i++)
            Assert.Equal(names[i], tabs[i].DisplayName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Close after reorder — M5a neighbor selection
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MoveTabThenCloseActive_CorrectNeighbor_NextIndex()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        vm.ActiveTab = tabs[1];

        // Move first tab to last: [tab1, tab2, tab0]
        vm.MoveTab(0, 2);

        // Close active tab (tabs[1], now at index 0)
        // Wait, after Move(0,2): [tab1(0), tab2(1), tab0(2)]
        // ActiveTab = tabs[1] at index 0. Close at index 0 → neighbor is index 0 = tabs[2] (was tab2)
        // Actually CloseTabAsync: removes at index 0, if index < new count → ActiveTab = OpenTabs[0]
        // After removal: [tab2(0), tab0(1)] → ActiveTab = OpenTabs[0] = tabs[2]

        await vm.CloseTabCommand.Execute(vm.ActiveTab!);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabs[2], vm.ActiveTab);
    }

    [Fact]
    public async Task MoveTabThenCloseActive_CorrectNeighbor_LastTab()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        vm.ActiveTab = tabs[0];

        // Move last to first: Move(2,0) from [tab0, tab1, tab2]
        // → [tab2(0), tab0(1), tab1(2)]
        vm.MoveTab(2, 0);

        // Close active tab (tabs[0]) at index 1.
        // RemoveAt(1) → [tab2(0), tab1(1)]. index(1) < Count(2) → neighbor = OpenTabs[1] = tabs[1]
        await vm.CloseTabCommand.Execute(vm.ActiveTab!);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabs[1], vm.ActiveTab);
    }

    [Fact]
    public async Task MoveTabThenCloseLast_ActivatesPreviousNeighbor()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 3);
        vm.ActiveTab = tabs[2];

        // Move(0,2) from [tab0(0), tab1(1), tab2(2)]
        // → [tab1(0), tab2(1), tab0(2)]. Active = tabs[2] at index 1.
        vm.MoveTab(0, 2);
        // Move(1,2) from [tab1(0), tab2(1), tab0(2)]
        // → [tab1(0), tab0(1), tab2(2)]. Active = tabs[2] at index 2 (last).
        vm.MoveTab(1, 2);

        // Close last tab — tabs[2] at index 2.
        // RemoveAt(2) → [tab1(0), tab0(1)]. index(2) >= Count(2) → previous = OpenTabs[1] = tabs[0]
        await vm.CloseTabCommand.Execute(vm.ActiveTab!);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabs[0], vm.ActiveTab);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Multiple moves
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MoveTab_MultipleMoves_NoCorruption()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 5);

        vm.MoveTab(0, 3); // [tab1, tab2, tab3, tab0, tab4]
        vm.MoveTab(4, 1); // move tab4 from index 4 to index 1
        // Before: [tab1(0), tab2(1), tab3(2), tab0(3), tab4(4)]
        // Move(4,1): Remove tab4 → [tab1, tab2, tab3, tab0], Insert at 1 → [tab1, tab4, tab2, tab3, tab0]
        vm.MoveTab(2, 4); // move tab2 (now at index 2) to end
        // Before: [tab1(0), tab4(1), tab2(2), tab3(3), tab0(4)]
        // Move(2,4): Remove tab2 → [tab1, tab4, tab3, tab0], Insert at 4 → [tab1, tab4, tab3, tab0, tab2]

        Assert.Equal(5, vm.OpenTabs.Count);
        Assert.Same(tabs[1], vm.OpenTabs[0]);
        Assert.Same(tabs[4], vm.OpenTabs[1]);
        Assert.Same(tabs[3], vm.OpenTabs[2]);
        Assert.Same(tabs[0], vm.OpenTabs[3]);
        Assert.Same(tabs[2], vm.OpenTabs[4]);
    }

    [Fact]
    public void MoveTab_ChainToReverseOrder_DoesNotThrow()
    {
        var vm = CreateViewModel();
        var tabs = AddTabs(vm, 4);

        // Reverse the list one move at a time
        vm.MoveTab(3, 0); // [tab3, tab0, tab1, tab2]
        vm.MoveTab(3, 1); // [tab3, tab2, tab0, tab1]
        vm.MoveTab(3, 2); // [tab3, tab2, tab1, tab0]

        Assert.Same(tabs[3], vm.OpenTabs[0]);
        Assert.Same(tabs[2], vm.OpenTabs[1]);
        Assert.Same(tabs[1], vm.OpenTabs[2]);
        Assert.Same(tabs[0], vm.OpenTabs[3]);
    }
}
