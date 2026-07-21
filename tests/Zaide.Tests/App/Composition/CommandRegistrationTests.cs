using System;
using System.Linq;
using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.App.Composition;
using Zaide.Tests;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
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
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;
using Zaide.Tests.App.Composition;

namespace Zaide.Tests.App.Composition;
/// <summary>
/// Composition: Verifies <c>palette.open</c> is registered exactly once with
/// M0-locked metadata, default gesture, and always-available semantics.
/// Duplicate-registration fail-fast is preserved.
/// </summary>
public sealed class CommandRegistrationTests
{
    static CommandRegistrationTests()
    {
        // ReactiveUI must be initialized before using WhenAnyValue in constructors.
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry NewRegistry() => CommandRegistryFactory.Create();

    // ── Metadata ─────────────────────────────────────────────────────────

    [Fact]
    public void PaletteOpen_IsRegisteredWithCorrectMetadata()
    {
        var registry = NewRegistry();
        _ = new CommandPaletteViewModel(registry);

        var descriptor = registry.GetById("palette.open");
        Assert.NotNull(descriptor);
        Assert.Equal("palette.open", descriptor!.Id);
        Assert.Equal("Open Command Palette", descriptor.DisplayName);
        Assert.Equal("Palette", descriptor.Category);
    }

    [Fact]
    public void PaletteOpen_HasCorrectDefaultGesture()
    {
        var registry = NewRegistry();
        _ = new CommandPaletteViewModel(registry);

        var descriptor = registry.GetById("palette.open");
        Assert.NotNull(descriptor);
        Assert.Equal(new[] { "Ctrl+Shift+P" }, descriptor!.DefaultGestures);
    }

    [Fact]
    public void PaletteOpen_IsAlwaysAvailable()
    {
        var registry = NewRegistry();
        _ = new CommandPaletteViewModel(registry);

        var descriptor = registry.GetById("palette.open");
        Assert.NotNull(descriptor);
        Assert.True(descriptor!.Command.CanExecute(null));
    }

    // ── Exactly-once registration ────────────────────────────────────────

    [Fact]
    public void PaletteOpen_RegisteredExactlyOnce()
    {
        var registry = NewRegistry();
        _ = new CommandPaletteViewModel(registry);

        var count = registry.GetAll().Count(d => d.Id == "palette.open");
        Assert.Equal(1, count);
    }

    [Fact]
    public void PaletteOpen_DuplicateRegistration_Throws()
    {
        var registry = NewRegistry();
        _ = new CommandPaletteViewModel(registry);

        // A second ViewModel with the same registry must fail.
        Assert.Throws<InvalidOperationException>(() =>
            new CommandPaletteViewModel(registry));
    }

    // ── Coexistence with Phase 8.2 canonical commands ─────────────────────

    [Fact]
    public void PaletteOpen_CoexistsWithCanonicalCommands()
    {
        var registry = NewRegistry();
        // Simulate the canonical registration (only commands that exist).
        CreateMainWindowViewModel(registry);
        _ = new CommandPaletteViewModel(registry);

        // palette.open is present alongside Phase 8.2 commands
        Assert.NotNull(registry.GetById("palette.open"));
        Assert.NotNull(registry.GetById("file.save"));
        Assert.NotNull(registry.GetById("workspace.openFolder"));
    }

    // ── Test construction helpers ────────────────────────────────────────

    /// <summary>
    /// Minimal MainWindowViewModel wiring for coexistence tests.
    /// Registers the four canonical window commands (file.save, workspace.openFolder,
    /// workspace.closeFolder, view.toggleBottomPanel).
    /// </summary>
    private static MainWindowViewModel CreateMainWindowViewModel(ICommandRegistry registry)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton<IEditorSessionFactory, EditorSessionFactory>()
            .AddSingleton<Workspace>()
            .BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(
            new FileTreeService(), CurrentThreadScheduler.Instance, registry);
        var editorTabs = new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateSourceControlViewModel(registry);
        var workspace = sp.GetRequiredService<Workspace>();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        return new MainWindowViewModel(
            fileTreeViewModel, editorTabs, terminalHost, panelHost,
            router, townhallViewModel, scViewModel,
            TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs, registry), workspace,
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, ConversationsTestSupport.CreateCatalogAsInterface(), registry);
    }

    private static SourceControlViewModel CreateSourceControlViewModel(ICommandRegistry registry)
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot());
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
            .Returns((FileDiffResult?)null);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();

        return new SourceControlViewModel(
            orchestrator, new Workspace(),
            mutation.Object, git.Object, commandRegistry: registry);
    }
}
