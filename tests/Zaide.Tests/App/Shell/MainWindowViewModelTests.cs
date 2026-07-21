using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.App.Composition;
using Zaide.Tests.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Agents.Infrastructure;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
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
using Zaide.Tests.Features.Agents;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.App.Shell;
public class MainWindowViewModelTests
{
    static MainWindowViewModelTests()
    {
        // ReactiveUI must be initialized before using WhenAnyValue in constructor
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static Mock<IAgentExecutionCoordinator> CreateMockCoordinator()
    {
        return new Mock<IAgentExecutionCoordinator>();
    }

    // Phase 8.3 M3: MainWindowViewModel now requires IProjectContextService.
    // These pre-M3 tests exercise unrelated behavior, so a loose mock satisfies
    // the constructor without driving discovery or projecting state.
    // M4: WhenChanged must return a non-null observable; the subscription in
    // Activate() calls ObserveOn which requires a non-null source.
    private static IProjectContextService ProjectContextServiceMock()
    {
        var mock = new Mock<IProjectContextService>(MockBehavior.Loose);
        mock.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
        return mock.Object;
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return CreateViewModel(new FileService());
    }

    private static MainWindowViewModel CreateViewModel(IFileService fileService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fileService);
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var factory = new Moq.Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();
        return vm;
    }

    private static MainWindowViewModel CreateViewModel(ITerminalHost terminalHost)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();
        return vm;
    }

    private static SourceControlViewModel CreateScViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();
        return new SourceControlViewModel(orchestrator, new Workspace(), mutation.Object, git.Object);
    }

    [Fact]
    public void InitialState_IsBottomPanelHidden()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsBottomPanelVisible);
    }

    [Fact]
    public void ToggleBottomPanel_TogglesVisibility()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsBottomPanelVisible);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.True(vm.IsBottomPanelVisible);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.False(vm.IsBottomPanelVisible);
    }

    [Fact]
    public async Task OpenFolderCommand_RefreshesSourceControlForNewWorkspace()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);

        var workspace = sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/repo", "/repo/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = new[] { new GitBranch("main", true) },
            Changes = Array.Empty<FileChange>(),
        });
        // Share the same Workspace instance the MainWindowViewModel mutates on open.
        var mutation = new Mock<IGitMutationService>();
        var scViewModel = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object), workspace, mutation.Object, git.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();

        // Before opening a folder the (empty) workspace yields no branch.
        Assert.Empty(scViewModel.Branches);

        var repoPath = Path.Combine(Path.GetTempPath(), "zaide-sctest-" + Guid.NewGuid());
        Directory.CreateDirectory(repoPath);
        try
        {
            vm.PickFolder.RegisterHandler(ctx => ctx.SetOutput(repoPath));
            vm.OpenFolderCommand.Execute(Unit.Default).Subscribe();
            await Task.Delay(150);

            // Opening the folder must refresh Source Control from the new workspace.
            Assert.Equal(repoPath, workspace.WorkspacePath);
            Assert.Equal(SnapshotRefreshStatus.Success, vm.SourceControlViewModel.LastRefreshStatus);
            Assert.Single(vm.SourceControlViewModel.Branches);
            Assert.Equal("main", vm.SourceControlViewModel.CurrentBranchName);
        }
        finally
        {
            if (Directory.Exists(repoPath))
                Directory.Delete(repoPath, recursive: true);
        }
    }

    [Fact]
    public async Task OpeningFolderViaFileTreeDirectly_SyncsWorkspaceAndRefreshesSourceControl()
    {
        // Regression guard: the file-tree "Open Folder..." header invokes
        // FileTreeViewModel.OpenFolderCommand directly (not MainWindowViewModel's
        // OpenFolderCommand). This path must still sync the shared workspace and
        // refresh Source Control; otherwise the panel reports "No repository"
        // even though a repository is open.
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);

        var workspace = sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/repo", "/repo/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = new[] { new GitBranch("main", true) },
            Changes = Array.Empty<FileChange>(),
        });
        var mutation = new Mock<IGitMutationService>();
        var scViewModel = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object), workspace, mutation.Object, git.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();

        Assert.Empty(scViewModel.Branches);

        var repoPath = Path.Combine(Path.GetTempPath(), "zaide-sctest-" + Guid.NewGuid());
        Directory.CreateDirectory(repoPath);
        try
        {
            // Simulate the file-tree header: open directly via the file tree,
            // bypassing MainWindowViewModel.OpenFolderCommand entirely.
            fileTreeViewModel.OpenFolderCommand.Execute(repoPath).Subscribe();
            await Task.Delay(150);

            Assert.Equal(repoPath, workspace.WorkspacePath);
            Assert.Equal(repoPath, vm.FileTreeViewModel.RootPath);
            Assert.Equal(SnapshotRefreshStatus.Success, vm.SourceControlViewModel.LastRefreshStatus);
            Assert.Single(vm.SourceControlViewModel.Branches);
            Assert.Equal("main", vm.SourceControlViewModel.CurrentBranchName);
        }
        finally
        {
            if (Directory.Exists(repoPath))
                Directory.Delete(repoPath, recursive: true);
        }
    }

    [Fact]
    public async Task SelectingSupportedFile_OpensActiveTabWithContent()
    {
        var vm = CreateViewModel();
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".cs");
        const string content = "class Program { }";

        try
        {
            File.WriteAllText(filePath, content);

            var node = new FileTreeNode
            {
                Name = Path.GetFileName(filePath),
                FullPath = filePath,
                IsDirectory = false
            };
            vm.FileTreeViewModel.SelectedFile = node;
            vm.FileTreeViewModel.RequestOpenFileCommand.Execute(node).Subscribe();
            await System.Threading.Tasks.Task.Delay(100);

            Assert.Single(vm.EditorTabs.OpenTabs);
            Assert.Same(vm.EditorTabs.OpenTabs[0], vm.EditorTabs.ActiveTab);
            Assert.Equal(content, vm.EditorTabs.ActiveTab!.TextContent);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SelectingUnreadableFile_ShowsOpenFailureStatus()
    {
        var fileService = new MockFileService
        {
            ReadException = new IOException("read denied")
        };
        var vm = CreateViewModel(fileService);

        var node = new FileTreeNode
        {
            Name = "Broken.cs",
            FullPath = "/tmp/Broken.cs",
            IsDirectory = false
        };
        vm.FileTreeViewModel.SelectedFile = node;
        vm.FileTreeViewModel.RequestOpenFileCommand.Execute(node).Subscribe();

        await Task.Delay(100);

        Assert.Empty(vm.EditorTabs.OpenTabs);
        Assert.Equal("Open failed: read denied", vm.StatusText);
    }

    [Fact]
    public async Task SaveActiveTabCommand_ShowsFailureStatus()
    {
        var fileService = new MockFileService
        {
            WriteException = new UnauthorizedAccessException("permission denied")
        };
        var vm = CreateViewModel(fileService);
        var editor = new EditorViewModel(new Document(""), fileService)
        {
            FilePath = "/tmp/test.txt",
            TextContent = "dirty"
        };
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        vm.SaveActiveTabCommand.Execute().Subscribe();
        await Task.Delay(50);

        Assert.Equal("Save failed: permission denied", vm.StatusText);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void InitialState_LeftPanelModeIsExplorer()
    {
        var vm = CreateViewModel();
        Assert.Equal(LeftPanelMode.Explorer, vm.LeftPanelMode);
        Assert.True(vm.IsExplorerMode);
        Assert.False(vm.IsSourceControlMode);
    }

    [Fact]
    public void SwitchToSourceControl_SetsModeToSourceControl()
    {
        var vm = CreateViewModel();
        Assert.Equal(LeftPanelMode.Explorer, vm.LeftPanelMode);

        vm.SwitchToSourceControlCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.SourceControl, vm.LeftPanelMode);
        Assert.False(vm.IsExplorerMode);
        Assert.True(vm.IsSourceControlMode);
    }

    [Fact]
    public void SwitchToExplorer_SetsModeToExplorer()
    {
        var vm = CreateViewModel();
        vm.SwitchToSourceControlCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.SourceControl, vm.LeftPanelMode);

        vm.SwitchToExplorerCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.Explorer, vm.LeftPanelMode);
        Assert.True(vm.IsExplorerMode);
        Assert.False(vm.IsSourceControlMode);
    }

    [Fact]
    public async Task TerminalStartupError_UpdatesStatusText()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        terminalService.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pty failed"));
        var factory2 = new Moq.Mock<ITerminalServiceFactory>();
        factory2.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost2 = new TerminalHost(factory2.Object);
        var townhallState2 = new TownhallState();
        var townhallViewModel2 = ConversationsTestSupport.CreateTownhallViewModel(townhallState2);
        var scViewModel2 = CreateScViewModel();
        var workspace2 = sp.GetRequiredService<Workspace>();
        var coordinator2 = CreateMockCoordinator().Object;
        var panelHost2 = ConversationsTestSupport.CreatePanelHost();
        var parser2 = new MentionParser();
        var router2 = new AgentRouter(parser2, panelHost2, coordinator2, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost2, panelHost2, router2, townhallViewModel2, scViewModel2, TestProblemsFactory.Create(workspace2, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace2, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();

        await terminalHost2.EnsureActiveSessionStartedAsync();

        Assert.Equal("Terminal: pty failed", vm.StatusText);
    }

    [Fact]
    public void ToggleBottomPanel_DoesNotDestroySessions()
    {
        var service = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(service.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var initialSession = terminalHost.ActiveSession;
        var vm = CreateViewModel(terminalHost);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        vm.ToggleBottomPanelCommand.Execute().Subscribe();

        service.Verify(s => s.Dispose(), Times.Never);
        Assert.Same(initialSession, terminalHost.ActiveSession);
    }

    // ── Phase 5.4 M2 / Phase 14 M4: Agent send without public Townhall mirror ──

    /// <summary>
    /// Creates a MainWindowViewModel with a real AgentPanelHost, real TownhallViewModel,
    /// and a mock IAgentExecutionCoordinator that appends user/assistant output on success.
    /// </summary>
    private static (MainWindowViewModel Vm, AgentPanelState Panel) CreateAgentSendTestViewModel(
        string statusOnCompletion = "Idle",
        bool appendAssistantOutput = true)
    {
        // Create panel
        var store = ConversationsTestSupport.CreateStore();
        var agentHost = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = agentHost.CreatePanel("agent-1", "Test Agent", "avatar_test");

        // Mock coordinator that simulates a successful or failed send
        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns<string, string, System.Threading.CancellationToken>((id, msg, _) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null)
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(null);

                if (appendAssistantOutput && statusOnCompletion != "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                    p.Status = statusOnCompletion;
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.SuccessResult(p));
                }

                if (appendAssistantOutput && statusOnCompletion == "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendError(store, p, msg);
                    p.Status = statusOnCompletion;
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.ErrorResult(p));
                }

                AgentPanelTestSupport.AppendUserChat(store, p, msg);
                p.Status = statusOnCompletion;
                p.IsBusy = false;
                return Task.FromResult<AgentExecutionCoordinatorResult?>(null);
            });

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var factory = new Moq.Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();
        return (vm, panel);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync does not mirror the user request into the
    /// active public channel.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_DoesNotMirrorUserIntoTownhall()
    {
        var (vm, panel) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello from test");

        Assert.Equal(beforeTownhallCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("User: Hello from test", panel.OutputHistory[0]);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync does not mirror the agent response into the
    /// active public channel after a successful send.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_DoesNotMirrorAgentResponseIntoTownhall()
    {
        var (vm, panel) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[^1]);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync does not mirror an AgentError into the
    /// active public channel when the panel ends in Error status.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_DoesNotMirrorErrorIntoTownhall()
    {
        var (vm, panel) = CreateAgentSendTestViewModel(statusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Error: Request failed", panel.OutputHistory[^1]);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync does not crash when the panel ID is unknown
    /// and does not add Townhall entries.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_UnknownPanel_DoesNotMirrorIntoTownhall()
    {
        var (vm, _) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync("non-existent-panel", "Hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    // ── Phase 5.4 M3: Alignment between panel-visible state and direct conversation ──

    /// <summary>
    /// Verifies that after a successful send, the panel OutputHistory contains the
    /// user request and agent response while Townhall channel history is unchanged.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ContentMatchesPanelOutput()
    {
        var (vm, panel) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello from alignment test");

        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello from alignment test", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
        Assert.Equal(beforeTownhallCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Verifies that after an error, the panel-visible error state is present and
    /// Townhall channel history is unchanged.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ErrorContentMatchesPanelOutput()
    {
        var (vm, panel) = CreateAgentSendTestViewModel(statusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Trigger error");

        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Trigger error", panel.OutputHistory[0]);
        Assert.Equal("Error: Request failed", panel.OutputHistory[1]);
        Assert.Equal("Error", panel.Status);
        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Verifies that when appendAssistantOutput=false on error, Townhall remains unchanged.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ErrorWithSingleOutput_DoesNotMirrorIntoTownhall()
    {
        var (vm, panel) = CreateAgentSendTestViewModel(statusOnCompletion: "Error", appendAssistantOutput: false);

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;
        await vm.SendAgentMessageAsync(panel.PanelId, "Single output error");

        Assert.Single(panel.OutputHistory);
        Assert.Equal("User: Single output error", panel.OutputHistory[0]);
        Assert.Equal("Error", panel.Status);
        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Verifies that when the coordinator returns no structured execution result,
    /// Townhall channel history is unchanged.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_NonAssistantResponse_DoesNotMirrorIntoTownhall()
    {
        var store = ConversationsTestSupport.CreateStore();
        var agentHost = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = agentHost.CreatePanel("agent-1", "Test Agent", "avatar_test");

        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns<string, string, System.Threading.CancellationToken>((id, msg, _) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null)
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(null);

                AgentPanelTestSupport.AppendUserChat(store, p, msg);
                p.Status = "Idle";
                p.IsBusy = false;
                return Task.FromResult<AgentExecutionCoordinatorResult?>(null);
            });

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var factory = new Moq.Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Non-standard response");

        // Panel has only the user entry because no structured terminal result was returned
        Assert.Single(panel.OutputHistory);
        Assert.Equal("User: Non-standard response", panel.OutputHistory[0]);

        // Only the user message should appear on the panel; Townhall unchanged.
        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Non-standard response", panel.OutputHistory[0].Replace("User: ", string.Empty));
    }

    /// <summary>
    /// Verifies panel Status after send while Townhall channel history stays unchanged.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_PanelStatusUpdatedWithoutTownhallMirror()
    {
        var (vm, panel) = CreateAgentSendTestViewModel(statusOnCompletion: "Idle");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Status alignment check");

        Assert.Equal("Idle", panel.Status);
        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync updates panel output only when successful.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_OrderIsUserThenResponseOnPanelOnly()
    {
        var (vm, panel) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Order check");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("User: Order check", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
    }

    // ── Phase 6.1 M1: Consume RouteResult in SendAgentMessageAsync ──────────────

    /// <summary>
    /// Creates a MainWindowViewModel with a real AgentPanelHost holding TWO panels
    /// (source "Alpha" and target "Beta"), a real TownhallViewModel, and a mock
    /// coordinator that appends output to whichever panel the router targets.
    /// </summary>
    private static (MainWindowViewModel Vm, AgentPanelState Source, AgentPanelState Target) CreateTwoPanelAgentSendTestViewModel(
        string targetStatusOnCompletion = "Idle",
        bool appendTargetOutput = true,
        Action<AgentPanelHost>? afterSend = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var agentHost = ConversationsTestSupport.CreatePanelHost(catalog, store);
        // Use catalog seed actors so @Beta resolves to the same panel/conversation.
        var source = agentHost.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var target = agentHost.GetOrCreatePanelForActor(ActorId.PanelSeed("beta"));

        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns<string, string, System.Threading.CancellationToken>((id, msg, _) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null)
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(null);

                if (appendTargetOutput && targetStatusOnCompletion != "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg, "Routed response");
                    p.Status = targetStatusOnCompletion;
                    afterSend?.Invoke(agentHost);
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.SuccessResult(p, "Routed response"));
                }

                if (appendTargetOutput && targetStatusOnCompletion == "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendError(store, p, msg, "Something failed");
                    p.Status = targetStatusOnCompletion;
                    afterSend?.Invoke(agentHost);
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.ErrorResult(p, "Something failed"));
                }

                AgentPanelTestSupport.AppendUserChat(store, p, msg);
                p.Status = targetStatusOnCompletion;
                p.IsBusy = false;
                afterSend?.Invoke(agentHost);
                return Task.FromResult<AgentExecutionCoordinatorResult?>(null);
            });

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var factory = new Moq.Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(
            townhallState,
            catalog: catalog,
            store: store);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object, catalog, store);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), catalog);
        vm.Activate();
        return (vm, source, target);
    }

    /// <summary>
    /// Case A: unknown mention target does not mirror routing failure into Townhall.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_UnknownTarget_DoesNotMirrorRoutingFailure()
    {
        var (vm, panel) = CreateAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "@NonExistentAgent hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Case A: multiple mentions does not mirror routing failure into Townhall.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_MultipleMentions_DoesNotMirrorRoutingFailure()
    {
        var (vm, source, _) = CreateTwoPanelAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Alpha @Beta hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    /// <summary>
    /// Case B: routed success updates target panel output without Townhall mirror.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_DoesNotMirrorTargetAssistantResponse()
    {
        var (vm, source, target) = CreateTwoPanelAgentSendTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Beta hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Assistant: Routed response", target.OutputHistory[^1]);
    }

    /// <summary>
    /// Case B: routed success where the target panel ends in Error does not mirror
    /// into Townhall.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_DoesNotMirrorTargetError()
    {
        var (vm, source, target) = CreateTwoPanelAgentSendTestViewModel(targetStatusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Beta hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Error: Something failed", target.OutputHistory[^1]);
    }

    /// <summary>
    /// Case B: routed success where the target panel has vanished before completion
    /// must not crash and must not add Townhall entries.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_VanishedTargetPanel_NoTownhallEntry()
    {
        AgentPanelState? target = null;
        var (vm, source, t) = CreateTwoPanelAgentSendTestViewModel(
            afterSend: host =>
            {
                if (target is not null)
                    host.Panels.Remove(target);
            });
        target = t;

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Beta hello");

        Assert.Equal(beforeCount, vm.TownhallViewModel.Messages.Count);
    }

    [Fact]
    public void HideBottomPanel_HidesPanelWithoutDestroyingLastSession()
    {
        var service = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(service.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var initialSession = terminalHost.ActiveSession;
        var vm = CreateViewModel(terminalHost);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.True(vm.IsBottomPanelVisible);

        vm.HideBottomPanelCommand.Execute().Subscribe();

        Assert.False(vm.IsBottomPanelVisible);
        service.Verify(s => s.Dispose(), Times.Never);
        Assert.Single(terminalHost.Tabs);
        Assert.Same(initialSession, terminalHost.ActiveSession);
    }

    // ── Phase 8.1.3 M3: Workspace Close Lifecycle ────────────────────────────

    private static (MainWindowViewModel Vm, Workspace Workspace, SourceControlViewModel ScVm, FileTreeViewModel FileTreeVm)
        CreateCloseFlowViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);

        var workspace = sp.GetRequiredService<Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/repo", "/repo/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = new[] { new GitBranch("main", true) },
            Changes = Array.Empty<FileChange>(),
        });
        var mutation = new Mock<IGitMutationService>();
        var scViewModel = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object), workspace, mutation.Object, git.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();
        return (vm, workspace, scViewModel, fileTreeViewModel);
    }

    [Fact]
    public async Task CloseFolderCommand_ClearsWorkspaceAndSourceControl()
    {
        var (vm, workspace, scViewModel, fileTreeVm) = CreateCloseFlowViewModel();

        var repoPath = Path.Combine(Path.GetTempPath(), "zaide-close-" + Guid.NewGuid());
        Directory.CreateDirectory(repoPath);
        try
        {
            fileTreeVm.OpenFolderCommand.Execute(repoPath).Subscribe();
            await Task.Delay(150);

            Assert.Equal(repoPath, workspace.WorkspacePath);
            Assert.Equal(repoPath, fileTreeVm.RootPath);

            vm.CloseFolderCommand.Execute().Subscribe();

            Assert.Null(fileTreeVm.RootPath);
            Assert.Null(workspace.WorkspacePath);
            Assert.Equal("Zaide", workspace.ProjectName);
            Assert.Empty(fileTreeVm.RootNodes);
            Assert.Equal(SnapshotRefreshStatus.NotARepository, scViewModel.LastRefreshStatus);
            Assert.Empty(scViewModel.Branches);
        }
        finally
        {
            if (Directory.Exists(repoPath))
                Directory.Delete(repoPath, recursive: true);
        }
    }

    [Fact]
    public void CloseFolderRequested_InteractionBridge_CompletesAndCloses()
    {
        var (vm, workspace, scViewModel, fileTreeVm) = CreateCloseFlowViewModel();

        var repoPath = Path.Combine(Path.GetTempPath(), "zaide-close-" + Guid.NewGuid());
        Directory.CreateDirectory(repoPath);
        try
        {
            fileTreeVm.OpenFolderCommand.Execute(repoPath).Subscribe();
            Assert.Equal(repoPath, fileTreeVm.RootPath);

            fileTreeVm.CloseFolderRequested.Handle(Unit.Default).Subscribe();

            Assert.Null(fileTreeVm.RootPath);
            Assert.Null(workspace.WorkspacePath);
        }
        finally
        {
            if (Directory.Exists(repoPath))
                Directory.Delete(repoPath, recursive: true);
        }
    }

    [Fact]
    public async Task CloseFolder_RetainsOpenDocuments()
    {
        var (vm, workspace, _, fileTreeVm) = CreateCloseFlowViewModel();

        var repoPath = Path.Combine(Path.GetTempPath(), "zaide-close-" + Guid.NewGuid());
        var filePath = Path.Combine(repoPath, "test.cs");
        Directory.CreateDirectory(repoPath);
        try
        {
            File.WriteAllText(filePath, "class Test { }");
            fileTreeVm.OpenFolderCommand.Execute(repoPath).Subscribe();
            await Task.Delay(150);

            vm.EditorTabs.OpenFileCommand.Execute(filePath).Subscribe();
            await Task.Delay(100);
            Assert.Single(vm.EditorTabs.OpenTabs);

            vm.CloseFolderCommand.Execute().Subscribe();

            Assert.Single(vm.EditorTabs.OpenTabs);
        }
        finally
        {
            if (Directory.Exists(repoPath))
                Directory.Delete(repoPath, recursive: true);
        }
    }

    [Fact]
    public void CloseFolderCommand_IsDisabledWhenNoFolderOpen()
    {
        var (vm, _, _, fileTreeVm) = CreateCloseFlowViewModel();

        Assert.Null(fileTreeVm.RootPath);
        Assert.False(vm.CloseFolderCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void CloseFolderRequested_CompletesWhenNoFolderOpen()
    {
        var (vm, workspace, _, fileTreeVm) = CreateCloseFlowViewModel();

        // No folder is open — the interaction must still complete without hanging
        Assert.Null(fileTreeVm.RootPath);

        fileTreeVm.CloseFolderRequested.Handle(Unit.Default).Subscribe();

        // Workspace remains in its initial closed state
        Assert.Null(workspace.WorkspacePath);
        Assert.Null(fileTreeVm.RootPath);
    }

    // ── Phase 8.1.7.1: Full-chain integration with real coordinator ─────────────

    /// <summary>
    /// Creates temp dir + SettingsService for full-chain tests.
    /// Caller must delete the returned path after use.
    /// </summary>
    private static (SettingsService SettingsService, TestSecretStore Secrets, string TempDir)
        CreateFullChainSettings()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "ZaideFullChain_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        var settingsPath = Path.Combine(tmpDir, "settings.json");
        var lkgPath = Path.Combine(tmpDir, "lkg.json");
        var tmpPath = Path.Combine(tmpDir, "tmp.json");
        var llm = new LlmSettings(BaseUrl: "https://api.test.com/v1", Model: "test-model", ApiKeySource: "secret-store");
        var model = SettingsModel.Defaults with { Llm = llm };
        var json = SettingsSerializer.Serialize(model);
        File.WriteAllText(settingsPath, json);
        var settingsService = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
        var secrets = new TestSecretStore();
        secrets.Set("llm.apiKey", "test-key");
        return (settingsService, secrets, tmpDir);
    }

    /// <summary>
    /// Creates the common ViewModel plumbing for full-chain tests.
    /// </summary>
    private static (MainWindowViewModel Vm, AgentPanelState Panel, string TempDir) BuildFullChainVm(
        IAgentExecutionService executionService)
    {
        var store = ConversationsTestSupport.CreateStore();
        var agentHost = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = agentHost.CreatePanel("agent-1", "Test Agent", "avatar_test");

        var parser = new MentionParser();
        var coordinator = AgentExecutionTestSupport.CreateCoordinator(agentHost, executionService, store);
        var router = new AgentRouter(parser, agentHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = ConversationsTestSupport.CreateTownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            router, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();

        return (vm, panel, string.Empty);
    }

    [Fact]
    public async Task SendAgentMessageAsync_FullChain_Success_UpdatesPanelOnly()
    {
        var (settings, secrets, tmpDir) = CreateFullChainSettings();
        try
        {
            var handler = new FakeMessageHandler(HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "Full chain reply" }, finish_reason = "stop" } }
                }));
            var httpClient = new HttpClient(handler);
            var executionService = new AgentExecutionService(httpClient, settings, secrets);
            var (vm, panel, _) = BuildFullChainVm(executionService);

            var channelId = vm.TownhallViewModel.Channels[0].Id;
            vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
            var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

            await vm.SendAgentMessageAsync(panel.PanelId, "Full chain test");

            Assert.Equal(2, panel.OutputHistory.Count);
            Assert.Equal("User: Full chain test", panel.OutputHistory[0]);
            Assert.Equal("Assistant: Full chain reply", panel.OutputHistory[1]);
            Assert.Equal("Idle", panel.Status);
            Assert.False(panel.IsBusy);
            Assert.Equal(beforeTownhallCount, vm.TownhallViewModel.Messages.Count);
        }
        finally
        {
            settings.Dispose();
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task SendAgentMessageAsync_FullChain_Failure_UpdatesPanelErrorOnly()
    {
        var (settings, secrets, tmpDir) = CreateFullChainSettings();
        try
        {
            var handler = new FakeMessageHandler(HttpStatusCode.Unauthorized,
                """
                {"error": {"message": "bad key"}}
                """);
            var httpClient = new HttpClient(handler);
            var executionService = new AgentExecutionService(httpClient, settings, secrets);
            var (vm, panel, _) = BuildFullChainVm(executionService);

            var channelId = vm.TownhallViewModel.Channels[0].Id;
            vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();
            var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

            await vm.SendAgentMessageAsync(panel.PanelId, "Trigger 401");

            Assert.Equal("Error", panel.Status);
            Assert.False(panel.IsBusy);
            Assert.Equal(2, panel.OutputHistory.Count);
            Assert.Contains("401", panel.OutputHistory[1]);
            Assert.Equal(beforeTownhallCount, vm.TownhallViewModel.Messages.Count);
        }
        finally
        {
            settings.Dispose();
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task SendAgentMessageAsync_FullChain_NetworkException_UpdatesPanelError()
    {
        var (settings, secrets, tmpDir) = CreateFullChainSettings();
        try
        {
            var handler = new FaultMessageHandler(new HttpRequestException("connection refused"));
            var httpClient = new HttpClient(handler);
            var executionService = new AgentExecutionService(httpClient, settings, secrets);
            var (vm, panel, _) = BuildFullChainVm(executionService);

            var channelId = vm.TownhallViewModel.Channels[0].Id;
            vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

            await vm.SendAgentMessageAsync(panel.PanelId, "Net fail");

            Assert.Equal("Error", panel.Status);
            Assert.False(panel.IsBusy);
            Assert.Contains("connection refused", panel.OutputHistory[1]);
        }
        finally
        {
            settings.Dispose();
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
