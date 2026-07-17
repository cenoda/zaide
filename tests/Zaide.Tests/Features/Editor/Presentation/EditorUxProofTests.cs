using System;
using System.Linq;
using System.Reflection;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Windows.Input;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 9 M0: Live-code and library proof test for Editor UX.
///
/// This test class verifies:
///   1. The exact AvaloniaEdit APIs available for search, folding, caret/selection,
///      document undo grouping, and undo stack.
///   2. The existing ownership graph baseline (no selection state, current command IDs,
///      MainWindowViewModel and StatusBarViewModel registration patterns).
///   3. Compile-time verification against the installed AvaloniaEdit assemblies.
///
/// No production behavior is tested. This is a compile-backed API proof only.
/// </summary>
public sealed class EditorUxProofTests
{
    static EditorUxProofTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    // ── 1. AvaloniaEdit assembly version proof ─────────────────────────────

    [Fact]
    public void AvaloniaEditAssembly_IsVersion12()
    {
        var asm = typeof(TextEditor).Assembly;
        var version = asm.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal(12, version!.Major);
    }

    // ── 2. AvaloniaEdit Search API availability ────────────────────────────

    [Fact]
    public void SearchPanel_TypeIsAvailable()
    {
        // Compile-time proof that AvaloniaEdit.Search is accessible transitively
        // without a direct package reference to Avalonia.AvaloniaEdit.
        var type = typeof(SearchPanel);
        Assert.NotNull(type);

        // Verify key members exist
        Assert.NotNull(type.GetMethod("Install", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(TextEditor) }, null));
        Assert.NotNull(type.GetMethod("Open"));
        Assert.NotNull(type.GetMethod("Close"));
        Assert.NotNull(type.GetMethod("FindNext", new[] { typeof(int) }));
        Assert.NotNull(type.GetMethod("FindPrevious"));
        Assert.NotNull(type.GetMethod("ReplaceNext"));
        Assert.NotNull(type.GetMethod("ReplaceAll"));
        Assert.NotNull(type.GetMethod("Reactivate"));

        // Verify key properties
        Assert.NotNull(type.GetProperty("SearchPattern"));
        Assert.NotNull(type.GetProperty("ReplacePattern"));
        Assert.NotNull(type.GetProperty("IsReplaceMode"));
        Assert.NotNull(type.GetProperty("MatchCase"));
        Assert.NotNull(type.GetProperty("WholeWords"));
        Assert.NotNull(type.GetProperty("UseRegex"));
        Assert.NotNull(type.GetProperty("IsClosed"));
        Assert.NotNull(type.GetProperty("IsOpened"));
    }

    [Fact]
    public void ISearchStrategy_TypeIsAvailable()
    {
        var type = typeof(ISearchStrategy);
        Assert.NotNull(type);
        Assert.NotNull(type.GetMethod("FindAll"));
        Assert.NotNull(type.GetMethod("FindNext"));
    }

    [Fact]
    public void SearchMode_EnumIsAvailable()
    {
        Assert.True(typeof(SearchMode).IsEnum);
    }

    // ── 3. AvaloniaEdit Folding API availability ───────────────────────────

    [Fact]
    public void FoldingManager_TypeIsAvailable()
    {
        // Compile-time proof that AvaloniaEdit.Folding is accessible transitively.
        var type = typeof(FoldingManager);
        Assert.NotNull(type);

        Assert.NotNull(type.GetMethod("Install", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(TextArea) }, null));
        Assert.NotNull(type.GetMethod("Uninstall", BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(type.GetMethod("Clear"));
        Assert.NotNull(type.GetMethod("CreateFolding", new[] { typeof(int), typeof(int) }));
        Assert.NotNull(type.GetMethod("RemoveFolding"));
        Assert.NotNull(type.GetMethod("UpdateFoldings"));

        Assert.NotNull(type.GetProperty("AllFoldings"));
    }

    [Fact]
    public void FoldingSection_TypeIsAvailable()
    {
        var type = typeof(FoldingSection);
        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("IsFolded"));
        Assert.NotNull(type.GetProperty("Title"));
        Assert.NotNull(type.GetProperty("TextContent"));
    }

    [Fact]
    public void NewFolding_TypeIsAvailable()
    {
        var type = typeof(NewFolding);
        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("StartOffset"));
        Assert.NotNull(type.GetProperty("EndOffset"));
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("DefaultClosed"));
    }

    [Fact]
    public void XmlFoldingStrategy_TypeIsAvailable()
    {
        var type = typeof(XmlFoldingStrategy);
        Assert.NotNull(type);
    }

    // ── 4. AbstractFoldingStrategy does NOT exist in 12.0.0 ────────────────

    [Fact]
    public void AbstractFoldingStrategy_DoesNotExist()
    {
        var asm = typeof(FoldingManager).Assembly;
        var abstractStrategy = asm.GetType("AvaloniaEdit.Folding.AbstractFoldingStrategy");
        Assert.Null(abstractStrategy);
    }

    // ── 5. Caret / Selection API availability ──────────────────────────────

    [Fact]
    public void Caret_TypeIsAvailable()
    {
        // Caret is used by EditorView; verify key member contracts.
        var type = typeof(Caret);
        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("Line"));
        Assert.NotNull(type.GetProperty("Column"));
        Assert.NotNull(type.GetProperty("Offset"));
        Assert.NotNull(type.GetProperty("Position"));

        // PositionChanged event (already subscribed by EditorView)
        Assert.NotNull(type.GetEvent("PositionChanged"));
    }

    [Fact]
    public void Selection_TypeIsAvailable()
    {
        var type = typeof(Selection);
        Assert.NotNull(type);

        Assert.NotNull(type.GetMethod("Create", new[] { typeof(TextArea), typeof(int), typeof(int) }));
        Assert.NotNull(type.GetMethod("GetText"));
        Assert.NotNull(type.GetMethod("ReplaceSelectionWithText", new[] { typeof(string) }));

        Assert.NotNull(type.GetProperty("IsEmpty"));
        Assert.NotNull(type.GetProperty("StartPosition"));
        Assert.NotNull(type.GetProperty("EndPosition"));
        Assert.NotNull(type.GetProperty("Length"));
    }

    [Fact]
    public void TextArea_SelectionEventExists()
    {
        var type = typeof(TextArea);
        Assert.NotNull(type);

        // SelectionChanged event — needed for M3 selection tracking
        Assert.NotNull(type.GetEvent("SelectionChanged"));
    }

    [Fact]
    public void TextArea_ClearSelectionMethodExists()
    {
        var type = typeof(TextArea);
        Assert.NotNull(type.GetMethod("ClearSelection"));
    }

    // ── 6. Document / Undo stack API availability ──────────────────────────

    [Fact]
    public void TextDocument_UndoStackIsAvailable()
    {
        var type = typeof(TextDocument);
        // TextDocument.UndoStack property (used for undo grouping)
        var prop = type.GetProperty("UndoStack");
        Assert.NotNull(prop);
        Assert.Equal(typeof(UndoStack), prop!.PropertyType);
    }

    [Fact]
    public void UndoStack_GroupingMethodsExist()
    {
        var type = typeof(UndoStack);
        Assert.NotNull(type);
        Assert.NotNull(type.GetMethod("StartUndoGroup", Type.EmptyTypes));
        Assert.NotNull(type.GetMethod("StartUndoGroup", new[] { typeof(object) }));
        Assert.NotNull(type.GetMethod("StartContinuedUndoGroup", new[] { typeof(object) }));
        Assert.NotNull(type.GetMethod("EndUndoGroup"));
        Assert.NotNull(type.GetProperty("CanUndo"));
        Assert.NotNull(type.GetProperty("CanRedo"));
    }

    [Fact]
    public void TextDocument_RunUpdateExists()
    {
        // RunUpdate returns IDisposable for BeginUpdate/EndUpdate grouping
        var type = typeof(TextDocument);
        Assert.NotNull(type.GetMethod("RunUpdate"));
    }

    // ── 7. Ownership graph baseline: EditorViewModel has no selection state ─

    [Fact]
    public void EditorViewModel_HasCaretState()
    {
        // Verify current caret state exists
        var type = typeof(EditorViewModel);
        Assert.NotNull(type.GetProperty("CaretLine"));
        Assert.NotNull(type.GetProperty("CaretColumn"));
    }

    [Fact]
    public void EditorViewModel_HasSelectionState()
    {
        // M0 baseline: selection fields do NOT exist yet.
        // Phase 9 M6: now they exist with zero defaults.
        var type = typeof(EditorViewModel);
        var startProp = type.GetProperty("SelectionStart");
        var lengthProp = type.GetProperty("SelectionLength");
        var textProp = type.GetProperty("SelectionText");
        Assert.NotNull(startProp);
        Assert.NotNull(lengthProp);
        Assert.NotNull(textProp);

        // Verify zero defaults.
        var vm = new EditorViewModel(new Document(""), new FileService());
        Assert.Equal(0, startProp!.GetValue(vm));
        Assert.Equal(0, lengthProp!.GetValue(vm));
        Assert.Null(textProp!.GetValue(vm));
    }

    // ── 8. StatusBarViewModel baseline: projects caret only ────────────────

    [Fact]
    public void StatusBarViewModel_HasCaretText()
    {
        var type = typeof(StatusBarViewModel);
        Assert.NotNull(type.GetProperty("CaretText"));
    }

    // ── 9. Command registry: all seven canonical commands exist ────────────

    [Fact]
    public void SevenCanonicalCommands_AreRegistered()
    {
        var registry = CommandRegistryFactory.Create();
        CreateMainWindowViewModel(registry);

        var expected = new[]
        {
            "file.save",
            "workspace.openFolder",
            "workspace.closeFolder",
            "view.toggleBottomPanel",
            "explorer.toggleHiddenFiles",
            "sourcecontrol.commit",
            "sourcecontrol.refresh"
        };

        Assert.Equal(7, registry.GetAll().Count);
        foreach (var id in expected)
            Assert.NotNull(registry.GetById(id));
    }

    // ── 10. MainWindowViewModel accepts optional ICommandRegistry ─────────

    [Fact]
    public void MainWindowViewModel_AcceptsOptionalCommandRegistry()
    {
        // Verify the constructor signature supports the optional commandRegistry parameter.
        var ctor = typeof(MainWindowViewModel)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .First();
        var parameters = ctor.GetParameters();
        var registryParam = parameters.FirstOrDefault(p => p.Name == "commandRegistry");
        Assert.NotNull(registryParam);
        Assert.True(registryParam!.IsOptional);
        Assert.Equal(typeof(ICommandRegistry), registryParam.ParameterType);
    }

    // ── 11. Keybinding materialization is called in MainWindow activation ──

    [Fact]
    public void MainWindow_MaterializeRegistryBindingsMethodExists()
    {
        var methods = typeof(MainWindow).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Instance);
        var materialize = methods.FirstOrDefault(m =>
            m.Name.Contains("MaterializeRegistryBindings"));
        Assert.NotNull(materialize);
    }

    // ── Helper: builds a MainWindowViewModel with registry registration ────

    private static MainWindowViewModel CreateMainWindowViewModel(ICommandRegistry registry)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddTransient<EditorViewModel>()
            .AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>()
            .BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(
            new FileTreeService(), CurrentThreadScheduler.Instance, registry);
        var editorTabs = new EditorTabViewModel(
            sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var panelHost = new AgentPanelHost();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot());
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();
        var scViewModel = new SourceControlViewModel(
            orchestrator, new global::Zaide.Features.Workspace.Domain.Workspace(), mutation.Object, git.Object, commandRegistry: registry);
        var workspace = sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>();

        return new MainWindowViewModel(
            fileTreeViewModel, editorTabs, terminalHost, panelHost, coordinator,
            router, townhallViewModel, scViewModel,
            TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace,
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, registry);
    }
}
