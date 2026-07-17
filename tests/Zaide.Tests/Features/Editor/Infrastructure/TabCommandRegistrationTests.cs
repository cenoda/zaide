using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;

namespace Zaide.Tests.Features.Editor.Infrastructure;

/// <summary>
/// Phase 9 M5a: Verifies tab lifecycle command IDs are registered exactly
/// once with M0-locked metadata, default gestures, and correct availability.
/// </summary>
public sealed class TabCommandRegistrationTests
{
    static TabCommandRegistrationTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry NewRegistry() => CommandRegistryFactory.Create();

    private static EditorTabViewModel CreateEditorTabs(ICommandRegistry registry)
    {
        var mockFs = new MockFileService();
        var ws = new global::Zaide.Features.Workspace.Domain.Workspace();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(mockFs);
        services.AddSingleton(ws);
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(sp, mockFs, ws, registry);
    }

    // ── Metadata ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tab.next", "Next Tab", "Tab")]
    [InlineData("tab.previous", "Previous Tab", "Tab")]
    [InlineData("tab.close", "Close Tab", "Tab")]
    [InlineData("tab.closeOthers", "Close Other Tabs", "Tab")]
    [InlineData("tab.closeAll", "Close All Tabs", "Tab")]
    public void TabCommands_RegisteredWithCorrectMetadata(string id, string displayName, string category)
    {
        var registry = NewRegistry();
        CreateEditorTabs(registry);

        var descriptor = registry.GetById(id);
        Assert.NotNull(descriptor);
        Assert.Equal(id, descriptor!.Id);
        Assert.Equal(displayName, descriptor.DisplayName);
        Assert.Equal(category, descriptor.Category);
    }

    // ── Default Gestures ─────────────────────────────────────────────────

    [Fact]
    public void TabCommands_HaveCorrectDefaultGestures()
    {
        var registry = NewRegistry();
        CreateEditorTabs(registry);

        Assert.Equal(new[] { "Ctrl+Tab" }, registry.GetById("tab.next")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+Shift+Tab" }, registry.GetById("tab.previous")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+W", "Ctrl+F4" }, registry.GetById("tab.close")!.DefaultGestures);
        Assert.Empty(registry.GetById("tab.closeOthers")!.DefaultGestures);
        Assert.Empty(registry.GetById("tab.closeAll")!.DefaultGestures);
    }

    // ── Exactly-once registration ────────────────────────────────────────

    [Fact]
    public void TabCommands_RegisteredExactlyOnce()
    {
        var registry = NewRegistry();
        CreateEditorTabs(registry);

        foreach (var id in new[] { "tab.next", "tab.previous", "tab.close", "tab.closeOthers", "tab.closeAll" })
        {
            Assert.Equal(1, registry.GetAll().Count(d => d.Id == id));
        }
    }

    [Fact]
    public void TabCommands_DuplicateRegistration_Throws()
    {
        var registry = NewRegistry();
        CreateEditorTabs(registry);

        Assert.Throws<InvalidOperationException>(() => CreateEditorTabs(registry));
    }

    // ── Availability via descriptor CanExecute ────────────────────────────

    [Fact]
    public void TabNext_NotAvailable_WhenNoTabs()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        tabs.ActiveTab = null;

        Assert.False(registry.GetById("tab.next")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabNext_Available_WithMultipleTabs()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        var tab1 = new EditorViewModel(new Document("/tmp/a.cs", ""), new MockFileService());
        var tab2 = new EditorViewModel(new Document("/tmp/b.cs", ""), new MockFileService());
        tabs.OpenTabs.Add(tab1);
        tabs.OpenTabs.Add(tab2);
        tabs.ActiveTab = tab1;

        Assert.True(registry.GetById("tab.next")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabPrevious_NotAvailable_WhenSingleTab()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        var tab1 = new EditorViewModel(new Document("/tmp/a.cs", ""), new MockFileService());
        tabs.OpenTabs.Add(tab1);
        tabs.ActiveTab = tab1;

        Assert.False(registry.GetById("tab.previous")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabCloseActive_NotAvailable_WhenNoActiveTab()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        tabs.ActiveTab = null;

        Assert.False(registry.GetById("tab.close")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabCloseActive_Available_WhenActiveTabExists()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        var tab = new EditorViewModel(new Document("/tmp/a.cs", ""), new MockFileService());
        tabs.OpenTabs.Add(tab);
        tabs.ActiveTab = tab;

        Assert.True(registry.GetById("tab.close")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabCloseOthers_NotAvailable_WhenSingleTab()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        var tab = new EditorViewModel(new Document("/tmp/a.cs", ""), new MockFileService());
        tabs.OpenTabs.Add(tab);
        tabs.ActiveTab = tab;

        Assert.False(registry.GetById("tab.closeOthers")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabCloseAll_NotAvailable_WhenNoActiveTab()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        tabs.ActiveTab = null;

        Assert.False(registry.GetById("tab.closeAll")!.Command.CanExecute(null));
    }

    [Fact]
    public void TabCloseAll_Available_WhenActiveTabExists()
    {
        var registry = NewRegistry();
        var tabs = CreateEditorTabs(registry);
        var tab = new EditorViewModel(new Document("/tmp/a.cs", ""), new MockFileService());
        tabs.OpenTabs.Add(tab);
        tabs.ActiveTab = tab;

        Assert.True(registry.GetById("tab.closeAll")!.Command.CanExecute(null));
    }

    // ── Coexistence with existing commands ────────────────────────────────

    [Fact]
    public void TabCommands_CoexistWithFoldingCommands()
    {
        var registry = NewRegistry();
        CreateEditorTabs(registry);

        // Tab commands
        Assert.NotNull(registry.GetById("tab.next"));
        Assert.NotNull(registry.GetById("tab.previous"));
        Assert.NotNull(registry.GetById("tab.close"));
        Assert.NotNull(registry.GetById("tab.closeOthers"));
        Assert.NotNull(registry.GetById("tab.closeAll"));

        // Folding commands coexist
        Assert.NotNull(registry.GetById("editor.foldToggle"));
        Assert.NotNull(registry.GetById("editor.foldAll"));
        Assert.NotNull(registry.GetById("editor.unfoldAll"));
    }
}
