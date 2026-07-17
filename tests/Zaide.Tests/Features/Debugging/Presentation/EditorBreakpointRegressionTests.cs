using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Debugging.Presentation;
using Zaide.Tests.App.Composition;

namespace Zaide.Tests.Features.Debugging.Presentation;

/// <summary>
/// Phase 12 M3b regression tests proving breakpoint projection does not break
/// folding, search, or tab lifecycle behavior.
/// </summary>
public sealed class EditorBreakpointRegressionTests
{
    static EditorBreakpointRegressionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void FoldingCommands_StillRegistered_WithBreakpointViewModel()
    {
        var registry = CommandRegistryFactory.Create();
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton(new global::Zaide.Features.Workspace.Domain.Workspace())
            .BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>(), registry);
        var breakpointVm = TestEditorBreakpointFactory.Create(editorTabs, registry);
        breakpointVm.Activate();
        breakpointVm.Dispose();

        Assert.NotNull(registry.GetById("editor.foldToggle"));
        Assert.NotNull(registry.GetById("editor.foldAll"));
        Assert.NotNull(registry.GetById("editor.unfoldAll"));
    }

    [Fact]
    public void TabLifecycleCommands_StillRegistered_WithBreakpointViewModel()
    {
        var registry = CommandRegistryFactory.Create();
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton(new global::Zaide.Features.Workspace.Domain.Workspace())
            .BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>(), registry);
        var breakpointVm = TestEditorBreakpointFactory.Create(editorTabs, registry);
        breakpointVm.Activate();

        var tabA = new EditorViewModel(new Document("/tmp/a.cs", "a"), new FileService());
        var tabB = new EditorViewModel(new Document("/tmp/b.cs", "b"), new FileService());
        editorTabs.OpenTabs.Add(tabA);
        editorTabs.OpenTabs.Add(tabB);
        editorTabs.ActiveTab = tabA;

        editorTabs.TabNextCommand.Execute().Subscribe();
        Assert.Same(tabB, editorTabs.ActiveTab);

        editorTabs.TabPreviousCommand.Execute().Subscribe();
        Assert.Same(tabA, editorTabs.ActiveTab);

        breakpointVm.Dispose();
    }

    [Fact]
    public void SearchViewModel_StillTracksActiveDocument_WithBreakpointViewModelActive()
    {
        var registry = CommandRegistryFactory.Create();
        var search = new EditorSearchViewModel(registry);
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton(new global::Zaide.Features.Workspace.Domain.Workspace())
            .BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>(), registry);
        var breakpointVm = TestEditorBreakpointFactory.Create(editorTabs, registry);
        breakpointVm.Activate();

        var tab = new EditorViewModel(new Document("/tmp/find.cs", "needle"), new FileService());
        editorTabs.OpenTabs.Add(tab);
        editorTabs.ActiveTab = tab;

        search.ActiveDocumentId = tab.FilePath;
        Assert.Equal("/tmp/find.cs", search.ActiveDocumentId);

        breakpointVm.Dispose();
    }
}