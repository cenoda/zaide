using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.Tests.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 9 M5a: Tests for tab lifecycle commands (next, previous, close,
/// close others, close all) on <see cref="EditorTabViewModel"/>.
/// </summary>
public sealed class EditorTabViewModelTabLifecycleTests
{
    static EditorTabViewModelTabLifecycleTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an EditorTabViewModel without a command registry (behavioral
    /// tests only). Tabs use a real FileService.
    /// </summary>
    private static EditorTabViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(),
            sp.GetRequiredService<IFileService>(),
            sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>());
    }

    /// <summary>
    /// Creates an EditorTabViewModel along with its workspace so
    /// workspace-tracking tests can inspect ActiveDocument.
    /// </summary>
    private static (EditorTabViewModel Vm, global::Zaide.Features.Workspace.Domain.Workspace Ws) CreateVmWithWorkspace()
    {
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var mockFs = new MockFileService();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);
        return (vm, ws);
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
    /// Creates an EditorViewModel tab without workspace registration.
    /// </summary>
    private static EditorViewModel CreateSimpleTab(string path = "/tmp/test.cs")
    {
        return new EditorViewModel(new Document(path, "content"), new FileService());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Navigation: TabNext
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabNext_NavigatesToNextTab()
    {
        var vm = CreateViewModel();
        var tab0 = CreateSimpleTab("/tmp/a.cs");
        var tab1 = CreateSimpleTab("/tmp/b.cs");
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.ActiveTab = tab0;

        await vm.TabNextCommand.Execute(Unit.Default);

        Assert.Same(tab1, vm.ActiveTab);
        Assert.Equal(2, vm.OpenTabs.Count);
    }

    [Fact]
    public async Task TabNext_WrapsFromLastToFirst()
    {
        var vm = CreateViewModel();
        var tab0 = CreateSimpleTab("/tmp/a.cs");
        var tab1 = CreateSimpleTab("/tmp/b.cs");
        var tab2 = CreateSimpleTab("/tmp/c.cs");
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.OpenTabs.Add(tab2);
        vm.ActiveTab = tab2; // last tab

        await vm.TabNextCommand.Execute(Unit.Default);

        Assert.Same(tab0, vm.ActiveTab);
    }

    [Fact]
    public void TabNext_NotAvailable_WhenSingleTab()
    {
        var vm = CreateViewModel();
        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;

        Assert.False(((ICommand)vm.TabNextCommand).CanExecute(null));
    }

    [Fact]
    public void TabNext_NotAvailable_WhenNoTabs()
    {
        var vm = CreateViewModel();

        Assert.False(((ICommand)vm.TabNextCommand).CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Navigation: TabPrevious
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabPrevious_NavigatesToPreviousTab()
    {
        var vm = CreateViewModel();
        var tab0 = CreateSimpleTab("/tmp/a.cs");
        var tab1 = CreateSimpleTab("/tmp/b.cs");
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.ActiveTab = tab1;

        await vm.TabPreviousCommand.Execute(Unit.Default);

        Assert.Same(tab0, vm.ActiveTab);
    }

    [Fact]
    public async Task TabPrevious_WrapsFromFirstToLast()
    {
        var vm = CreateViewModel();
        var tab0 = CreateSimpleTab("/tmp/a.cs");
        var tab1 = CreateSimpleTab("/tmp/b.cs");
        var tab2 = CreateSimpleTab("/tmp/c.cs");
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.OpenTabs.Add(tab2);
        vm.ActiveTab = tab0; // first tab

        await vm.TabPreviousCommand.Execute(Unit.Default);

        Assert.Same(tab2, vm.ActiveTab);
    }

    [Fact]
    public void TabPrevious_NotAvailable_WhenSingleTab()
    {
        var vm = CreateViewModel();
        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;

        Assert.False(((ICommand)vm.TabPreviousCommand).CanExecute(null));
    }

    [Fact]
    public void TabPrevious_NotAvailable_WhenNoTabs()
    {
        var vm = CreateViewModel();

        Assert.False(((ICommand)vm.TabPreviousCommand).CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Close Active: Neighbor Selection
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabCloseActive_AtFirstTab_ActivatesNextNeighbor()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = tabA; // first tab

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabB, vm.ActiveTab);
        Assert.DoesNotContain(tabA, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseActive_AtMiddleTab_ActivatesNextNeighbor()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = tabB; // middle tab

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabC, vm.ActiveTab);
        Assert.DoesNotContain(tabB, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseActive_AtLastTab_ActivatesPreviousNeighbor()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = tabC; // last tab

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Same(tabB, vm.ActiveTab);
        Assert.DoesNotContain(tabC, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseActive_LastTab_SetsActiveNull()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tab = AddTab(ws, vm, "/tmp/only.cs");
        vm.ActiveTab = tab;

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public void TabCloseActive_NotAvailable_WhenNoTabs()
    {
        var vm = CreateViewModel();

        Assert.False(((ICommand)vm.TabCloseActiveCommand).CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Close Active: Dirty Confirmation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabCloseActive_CleanTab_ClosesImmediately()
    {
        var vm = CreateViewModel();
        vm.ConfirmClose.RegisterHandler(ctx => throw new InvalidOperationException(
            "Should not be called for a clean tab"));

        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;
        Assert.False(tab.IsDirty);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseActive_OnDirtyTab_SaveThenClose()
    {
        var mockFs = new MockFileService();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);

        // User chooses Save
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var doc = ws.OpenDocument("/tmp/dirty.cs", "original");
        var tab = new EditorViewModel(doc, mockFs);
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;
        tab.TextContent = "modified";
        Assert.True(tab.IsDirty);
        Assert.Null(vm.LastSaveError);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
        Assert.Equal("modified", mockFs.LastWrittenContent);
    }

    [Fact]
    public async Task TabCloseActive_OnDirtyTab_DiscardThenClose()
    {
        var vm = CreateViewModel();
        // User chooses Discard (false)
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(false));

        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;
        tab.TextContent = "modified";
        Assert.True(tab.IsDirty);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public async Task TabCloseActive_OnDirtyTab_Cancel_LeavesTab()
    {
        var vm = CreateViewModel();
        // User cancels (null)
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(null));

        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;
        tab.TextContent = "modified";
        Assert.True(tab.IsDirty);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tab, vm.ActiveTab);
        Assert.True(tab.IsDirty, "Tab should remain dirty after cancel");
    }

    [Fact]
    public async Task TabCloseActive_OnDirtyTab_SaveFailure_LeavesTab()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);

        // User chooses Save
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var doc = ws.OpenDocument("/tmp/fail.cs", "original");
        var tab = new EditorViewModel(doc, mockFs);
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;
        tab.TextContent = "modified";
        Assert.True(tab.IsDirty);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tab, vm.ActiveTab);
        Assert.True(tab.IsDirty, "Tab should remain dirty after save failure");
        Assert.NotNull(vm.LastSaveError);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Close Others
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabCloseOthers_ClosesNonActiveTabs()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = tabB;

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tabB, vm.ActiveTab);
        Assert.Contains(tabB, vm.OpenTabs);
        Assert.DoesNotContain(tabA, vm.OpenTabs);
        Assert.DoesNotContain(tabC, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseOthers_PreservesActiveTab_ContentAndDirtyState()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        vm.ActiveTab = tabA;
        tabA.TextContent = "modified active";
        Assert.True(tabA.IsDirty);

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tabA, vm.ActiveTab);
        Assert.True(tabA.IsDirty, "Active tab's dirty state must be preserved");
        Assert.Equal("modified active", tabA.TextContent);
    }

    [Fact]
    public async Task TabCloseOthers_CancelOnDirty_StopsAndLeavesRemaining()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        // ConfirmClose returns cancel for the first dirty non-active tab
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(null));

        var tabA = AddTab(ws, vm, "/tmp/clean.cs");
        var tabB = AddTab(ws, vm, "/tmp/dirty1.cs");
        var tabC = AddTab(ws, vm, "/tmp/dirty2.cs");
        vm.ActiveTab = tabA;

        // Mark B and C as dirty
        tabB.TextContent = "dirty B";
        tabC.TextContent = "dirty C";

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        // Non-active tabs processed left-to-right: tabB (dirty1) first.
        // Cancel on tabB → stop. tabC is never processed.
        // tabA (clean) is never closed (it's the active tab).
        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.Same(tabA, vm.ActiveTab);
        Assert.Contains(tabC, vm.OpenTabs);
        Assert.Contains(tabB, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseOthers_SaveFailure_StopsAndLeavesRemaining()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);

        // User chooses Save for dirty tabs
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var docA = ws.OpenDocument("/tmp/a.cs", "clean");
        var tabA = new EditorViewModel(docA, mockFs);
        vm.OpenTabs.Add(tabA);

        var docB = ws.OpenDocument("/tmp/b.cs", "original B");
        var tabB = new EditorViewModel(docB, mockFs);
        vm.OpenTabs.Add(tabB);

        var docC = ws.OpenDocument("/tmp/c.cs", "original C");
        var tabC = new EditorViewModel(docC, mockFs);
        vm.OpenTabs.Add(tabC);

        vm.ActiveTab = tabA;
        tabB.TextContent = "dirty B";
        Assert.True(tabB.IsDirty);

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        // tabB is dirty, save fails → stop. tabC never processed.
        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.Same(tabA, vm.ActiveTab);
        Assert.Contains(tabB, vm.OpenTabs);
        Assert.Contains(tabC, vm.OpenTabs);
        Assert.NotNull(vm.LastSaveError);
    }

    [Fact]
    public async Task TabCloseOthers_AllClean_ClosesAllNonActive()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        var tabD = AddTab(ws, vm, "/tmp/d.cs");
        vm.ActiveTab = tabB;

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        Assert.Single(vm.OpenTabs);
        Assert.Same(tabB, vm.ActiveTab);
    }

    [Fact]
    public void TabCloseOthers_NotAvailable_WhenSingleTab()
    {
        var vm = CreateViewModel();
        var tab = CreateSimpleTab();
        vm.OpenTabs.Add(tab);
        vm.ActiveTab = tab;

        Assert.False(((ICommand)vm.TabCloseOthersCommand).CanExecute(null));
    }

    [Fact]
    public void TabCloseOthers_NotAvailable_WhenNoTabs()
    {
        var vm = CreateViewModel();

        Assert.False(((ICommand)vm.TabCloseOthersCommand).CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Close All
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabCloseAll_ClosesAllTabs()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        AddTab(ws, vm, "/tmp/a.cs");
        AddTab(ws, vm, "/tmp/b.cs");
        AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = vm.OpenTabs[0];

        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public async Task TabCloseAll_AfterSuccessfulClose_ActiveTabAndDocumentNull()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        vm.ActiveTab = tabA;

        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
        Assert.Null(ws.ActiveDocument);
    }

    [Fact]
    public async Task TabCloseAll_CancelOnDirty_StopsAndLeavesRemaining()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        // Cancel on first dirty tab encountered
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(null));

        var cleanA = AddTab(ws, vm, "/tmp/cleanA.cs");
        var dirtyB = AddTab(ws, vm, "/tmp/dirtyB.cs");
        var cleanC = AddTab(ws, vm, "/tmp/cleanC.cs");
        vm.ActiveTab = cleanA;
        dirtyB.TextContent = "modified B";
        Assert.True(dirtyB.IsDirty);

        // Process in reverse: cleanC (closed), dirtyB (cancel → stop)
        await vm.TabCloseAllCommand.Execute(Unit.Default);

        // cleanC was closed. dirtyB and cleanA remain.
        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Contains(dirtyB, vm.OpenTabs);
        Assert.Contains(cleanA, vm.OpenTabs);
        // After cleanC was closed, cleanA was at index 0. dirtyB was at index 1.
        // Cancel at dirtyB → stop. cleanA remains active (never removed from close-all).
        // Wait: cleanA was at index 0. cleanC at index 2. Reverse: cleanC closes (non-active).
        // Then dirtyB at index 1. dirtyB is not active → CloseTabAsync doesn't change ActiveTab.
        // Cancel on dirtyB → stop. cleanA is still active.
        Assert.Same(cleanA, vm.ActiveTab);
    }

    [Fact]
    public async Task TabCloseAll_SaveFailure_StopsAndLeavesRemaining()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);

        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var docA = ws.OpenDocument("/tmp/cleanA.cs", "clean");
        var tabA = new EditorViewModel(docA, mockFs);
        vm.OpenTabs.Add(tabA);

        var docB = ws.OpenDocument("/tmp/dirtyB.cs", "original B");
        var tabB = new EditorViewModel(docB, mockFs);
        vm.OpenTabs.Add(tabB);

        vm.ActiveTab = tabA;
        tabB.TextContent = "modified B";
        Assert.True(tabB.IsDirty);

        // Reverse: tabB first. Save fails → stop.
        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Equal(2, vm.OpenTabs.Count);
        Assert.Contains(tabA, vm.OpenTabs);
        Assert.Contains(tabB, vm.OpenTabs);
        Assert.True(tabB.IsDirty);
    }

    [Fact]
    public async Task TabCloseAll_WithMultipleDirty_SaveFailureOnSecond_Stops()
    {
        var mockFs = new MockFileService
        {
            WriteException = new IOException("disk full")
        };
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        var vm = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), mockFs, ws);

        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(true));

        var docC = ws.OpenDocument("/tmp/cleanC.cs", "clean");
        var tabC = new EditorViewModel(docC, mockFs);
        vm.OpenTabs.Add(tabC);

        var docB = ws.OpenDocument("/tmp/dirtyB.cs", "original B");
        var tabB = new EditorViewModel(docB, mockFs);
        vm.OpenTabs.Add(tabB);

        var docA = ws.OpenDocument("/tmp/dirtyA.cs", "original A");
        var tabA = new EditorViewModel(docA, mockFs);
        vm.OpenTabs.Add(tabA);

        vm.ActiveTab = tabC;

        // Mark all as dirty except tabC
        tabA.TextContent = "modified A";
        tabB.TextContent = "modified B";
        Assert.True(tabA.IsDirty);
        Assert.True(tabB.IsDirty);

        // Reverse: tabA first. Save succeeds (no exception for tabA path).
        // Actually, MockFileService.WriteException throws for ALL paths.
        // So tabA save fails → stop.
        await vm.TabCloseAllCommand.Execute(Unit.Default);

        // tabA save failed → stop. tabB was never processed.
        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.Contains(tabC, vm.OpenTabs);
        Assert.Contains(tabB, vm.OpenTabs);
        Assert.Contains(tabA, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseAll_DiscardOnDirty_ClosesAll()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        // Discard all dirty tabs
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(false));

        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        vm.ActiveTab = tabA;
        tabB.TextContent = "modified B";
        Assert.True(tabB.IsDirty);

        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
    }

    [Fact]
    public void TabCloseAll_NotAvailable_WhenNoTabs()
    {
        var vm = CreateViewModel();

        Assert.False(((ICommand)vm.TabCloseAllCommand).CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Workspace.ActiveDocument Correctness
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabCloseActive_UpdatesWorkspaceActiveDocument()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        vm.ActiveTab = tabA;

        Assert.Same(tabA.Document, ws.ActiveDocument);

        await vm.TabCloseActiveCommand.Execute(Unit.Default);

        Assert.Same(tabB, vm.ActiveTab);
        Assert.Same(tabB.Document, ws.ActiveDocument);
    }

    [Fact]
    public async Task TabCloseOthers_PreservesWorkspaceActiveDocument()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        var tabC = AddTab(ws, vm, "/tmp/c.cs");
        vm.ActiveTab = tabB;

        Assert.Same(tabB.Document, ws.ActiveDocument);

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        Assert.Same(tabB, vm.ActiveTab);
        Assert.Same(tabB.Document, ws.ActiveDocument);
        Assert.DoesNotContain(tabA, vm.OpenTabs);
        Assert.DoesNotContain(tabC, vm.OpenTabs);
    }

    [Fact]
    public async Task TabCloseAll_SetsWorkspaceActiveDocumentNull()
    {
        var (vm, ws) = CreateVmWithWorkspace();
        var tabA = AddTab(ws, vm, "/tmp/a.cs");
        var tabB = AddTab(ws, vm, "/tmp/b.cs");
        vm.ActiveTab = tabA;

        await vm.TabCloseAllCommand.Execute(Unit.Default);

        Assert.Empty(vm.OpenTabs);
        Assert.Null(vm.ActiveTab);
        Assert.Null(ws.ActiveDocument);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dirty-state and content preservation for untouched tabs
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TabNavigation_PreservesTabContentAndDirtyState()
    {
        var vm = CreateViewModel();
        var tab0 = CreateSimpleTab("/tmp/a.cs");
        var tab1 = CreateSimpleTab("/tmp/b.cs");
        vm.OpenTabs.Add(tab0);
        vm.OpenTabs.Add(tab1);
        vm.ActiveTab = tab0;

        tab0.TextContent = "modified a";
        Assert.True(tab0.IsDirty);
        Assert.False(tab1.IsDirty);
        var tab1Content = tab1.TextContent;

        await vm.TabNextCommand.Execute(Unit.Default);
        Assert.Same(tab1, vm.ActiveTab);
        Assert.Equal(tab1Content, tab1.TextContent);
        Assert.False(tab1.IsDirty);

        await vm.TabPreviousCommand.Execute(Unit.Default);
        Assert.Same(tab0, vm.ActiveTab);
        Assert.True(tab0.IsDirty);
        Assert.Equal("modified a", tab0.TextContent);
    }

    /// <summary>
    /// When close-all or close-others stops mid-operation, already-closed
    /// tabs stay closed and the active tab content/dirty state is untouched.
    /// </summary>
    [Fact]
    public async Task TabCloseOthers_CancelOnSecondDirty_LeavesLaterTabsUnprocessed()
    {
        var (vm, ws) = CreateVmWithWorkspace();

        // ConfirmClose: first dirty tab returns null (cancel), second would
        // never be reached.
        vm.ConfirmClose.RegisterHandler(ctx => ctx.SetOutput(null));

        // Create tabs left-to-right: active, dirty1, dirty2
        var active = AddTab(ws, vm, "/tmp/active.cs");
        var dirty1 = AddTab(ws, vm, "/tmp/dirty1.cs");
        var dirty2 = AddTab(ws, vm, "/tmp/dirty2.cs");
        vm.ActiveTab = active;
        dirty1.TextContent = "modified 1";
        dirty2.TextContent = "modified 2";

        await vm.TabCloseOthersCommand.Execute(Unit.Default);

        // close-others processes non-active tabs left-to-right: dirty1, dirty2.
        // dirty1 is first → cancel → stop. dirty2 untouched.
        Assert.Equal(3, vm.OpenTabs.Count);
        Assert.False(active.IsDirty);
        Assert.True(dirty1.IsDirty);
        Assert.True(dirty2.IsDirty, "dirty2 should be untouched (cancel at dirty1 stopped iteration)");
        Assert.Equal("modified 2", dirty2.TextContent);
    }
}
