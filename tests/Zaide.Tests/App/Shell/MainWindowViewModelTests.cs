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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
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

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
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

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, townhallViewModel, scViewModel, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost2, townhallViewModel2, scViewModel2, TestProblemsFactory.Create(workspace2, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace2, ProjectContextServiceMock(), ConversationsTestSupport.CreateCatalogAsInterface());
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
}
