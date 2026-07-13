using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Tests;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.2 M8a: canonical command registration from the owning ViewModel
/// constructors. Verifies ownership, exactly-once registration, D6a metadata,
/// unbound commands, aliases, and duplicate-registration fail-fast. No gesture
/// parsing or resolution is tested here (M8b/M8c own that).
/// </summary>
public sealed class CanonicalCommandRegistrationTests
{
    static CanonicalCommandRegistrationTests()
    {
        // ReactiveUI must be initialized before using WhenAnyValue in constructors.
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry NewRegistry() => CommandRegistryFactory.Create();

    // ── Each owning ViewModel registers its expected command IDs ──────────

    [Fact]
    public void MainWindowViewModel_RegistersFourCanonicalCommands()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        Assert.NotNull(registry.GetById("file.save"));
        Assert.NotNull(registry.GetById("workspace.openFolder"));
        Assert.NotNull(registry.GetById("workspace.closeFolder"));
        Assert.NotNull(registry.GetById("view.toggleBottomPanel"));
    }

    [Fact]
    public void FileTreeViewModel_RegistersToggleHiddenFiles()
    {
        var registry = NewRegistry();
        _ = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance, registry);

        Assert.NotNull(registry.GetById("explorer.toggleHiddenFiles"));
    }

    [Fact]
    public void SourceControlViewModel_RegistersCommitAndRefresh()
    {
        var registry = NewRegistry();
        CreateSourceControlViewModel(registry);

        Assert.NotNull(registry.GetById("sourcecontrol.commit"));
        Assert.NotNull(registry.GetById("sourcecontrol.refresh"));
    }

    [Fact]
    public void ProjectWorkflowViewModel_RegistersBuildCancelAndTest()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        Assert.NotNull(registry.GetById("project.build"));
        Assert.NotNull(registry.GetById("project.cancel"));
        Assert.NotNull(registry.GetById("project.test"));
    }

    // ── All canonical IDs from MainWindow composition are present exactly once ──

    [Fact]
    public void AllSevenCanonicalCommands_PresentExactlyOnce()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry); // constructs all owning VMs, sharing the registry

        var expected = new[]
        {
            "file.save",
            "workspace.openFolder",
            "workspace.closeFolder",
            "view.toggleBottomPanel",
            "explorer.toggleHiddenFiles",
            "sourcecontrol.commit",
            "sourcecontrol.refresh",
            "project.build",
            "project.run",
            "project.test",
            "project.cancel",
        };

        Assert.Equal(11, registry.GetAll().Count);
        foreach (var id in expected)
        {
            Assert.NotNull(registry.GetById(id));
            Assert.Equal(1, registry.GetAll().Count(d => d.Id == id));
        }
    }

    // ── Descriptor display names / categories / default gestures (D6a) ───

    [Theory]
    [InlineData("file.save", "Save", "File")]
    [InlineData("workspace.openFolder", "Open Folder", "Workspace")]
    [InlineData("workspace.closeFolder", "Close Folder", "Workspace")]
    [InlineData("view.toggleBottomPanel", "Toggle Bottom Panel", "View")]
    [InlineData("explorer.toggleHiddenFiles", "Toggle Hidden Files", "Explorer")]
    [InlineData("sourcecontrol.commit", "Commit", "Source Control")]
    [InlineData("sourcecontrol.refresh", "Refresh", "Source Control")]
    [InlineData("project.run", "Run", "Project")]
    [InlineData("project.test", "Run Tests", "Project")]
    public void Descriptor_MetadataMatchesD6a(string id, string displayName, string category)
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        var descriptor = registry.GetById(id);
        Assert.NotNull(descriptor);
        Assert.Equal(displayName, descriptor!.DisplayName);
        Assert.Equal(category, descriptor.Category);
    }

    [Fact]
    public void DefaultGestures_MatchD6a()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        Assert.Equal(new[] { "Ctrl+S" }, registry.GetById("file.save")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+O" }, registry.GetById("workspace.openFolder")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+Oem3", "Ctrl+J" }, registry.GetById("view.toggleBottomPanel")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+Shift+H" }, registry.GetById("explorer.toggleHiddenFiles")!.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+F5" }, registry.GetById("project.run")!.DefaultGestures);
    }

    // ── Unbound commands ─────────────────────────────────────────────────

    [Fact]
    public void UnboundCommands_HaveEmptyDefaultGestures()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        Assert.Empty(registry.GetById("workspace.closeFolder")!.DefaultGestures);
        Assert.Empty(registry.GetById("sourcecontrol.commit")!.DefaultGestures);
        Assert.Empty(registry.GetById("sourcecontrol.refresh")!.DefaultGestures);
        Assert.Empty(registry.GetById("project.test")!.DefaultGestures);
        Assert.Empty(registry.GetById("project.cancel")!.DefaultGestures);
    }

    // ── view.toggleBottomPanel aliases ───────────────────────────────────

    [Fact]
    public void ViewToggleBottomPanel_HasBothAliases()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry);

        var gestures = registry.GetById("view.toggleBottomPanel")!.DefaultGestures
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "Ctrl+J", "Ctrl+Oem3" }, gestures);
    }

    // ── Duplicate registration still fails fast ──────────────────────────

    [Fact]
    public void DuplicateRegistration_StillThrows()
    {
        var registry = NewRegistry();
        CreateMainWindowViewModel(registry); // registers file.save

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new CommandDescriptor(
                "file.save", "Save", "File", Array.Empty<string>(), new AlwaysEnabledCommandStub())));

        Assert.Contains("file.save", ex.Message);
    }

    // ── Test construction helpers (DI-style; no second registration path) ──

    private static MainWindowViewModel CreateMainWindowViewModel(ICommandRegistry registry)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddTransient<EditorViewModel>()
            .AddSingleton<Workspace>()
            .BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance, registry);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateSourceControlViewModel(registry);
        var workspace = sp.GetRequiredService<Workspace>();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);

        return new MainWindowViewModel(
            fileTreeViewModel, editorTabs, terminalHost, panelHost, coordinator,
            router, townhallViewModel, scViewModel,
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(registry: registry),
            TestTestResultsFactory.Create(editorTabs),
            workspace,
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, registry);
    }

    private static SourceControlViewModel CreateSourceControlViewModel(ICommandRegistry registry)
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>())).Returns((FileDiffResult?)null);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();

        return new SourceControlViewModel(orchestrator, new Workspace(), diffService.Object, mutation.Object, git.Object, registry);
    }

    private sealed class AlwaysEnabledCommandStub : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
