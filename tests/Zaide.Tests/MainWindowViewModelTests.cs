using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Tests.Services;
using Zaide.ViewModels;

namespace Zaide.Tests;

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

    private static MainWindowViewModel CreateViewModel()
    {
        return CreateViewModel(new FileService());
    }

    private static MainWindowViewModel CreateViewModel(IFileService fileService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fileService);
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Zaide.Models.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Models.Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Moq.Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, coordinator, router, townhallViewModel, scViewModel, workspace);
        vm.Activate();
        return vm;
    }

    private static MainWindowViewModel CreateViewModel(ITerminalHost terminalHost)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Zaide.Models.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Models.Workspace>());
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, coordinator, router, townhallViewModel, scViewModel, workspace);
        vm.Activate();
        return vm;
    }

    private static SourceControlViewModel CreateScViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        return new SourceControlViewModel(orchestrator, new Workspace());
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
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Zaide.Models.Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeService = new FileTreeService();
        var fileTreeViewModel = new FileTreeViewModel(fileTreeService, CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Zaide.Models.Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);

        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);

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
        var scViewModel = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object), workspace);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, panelHost, coordinator, router, townhallViewModel, scViewModel, workspace);
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
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        terminalService.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pty failed"));
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory2 = new Moq.Mock<ITerminalSessionFactory>();
        factory2.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost2 = new TerminalHost(factory2.Object);
        var townhallState2 = new TownhallState();
        var townhallViewModel2 = new TownhallViewModel(townhallState2);
        var scViewModel2 = CreateScViewModel();
        var workspace2 = sp.GetRequiredService<Workspace>();
        var coordinator2 = CreateMockCoordinator().Object;
        var panelHost2 = new AgentPanelHost();
        var parser2 = new MentionParser(panelHost2);
        var router2 = new AgentRouter(parser2, panelHost2, coordinator2);
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost2, panelHost2, coordinator2, router2, townhallViewModel2, scViewModel2, workspace2);
        vm.Activate();

        await terminalHost2.EnsureActiveSessionStartedAsync();

        Assert.Equal("Terminal: pty failed", vm.StatusText);
    }

    [Fact]
    public void ToggleBottomPanel_DoesNotDestroySessions()
    {
        var service = new Mock<ITerminalService>();
        var terminalVm = new TerminalViewModel(service.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);
        var terminalHost = new TerminalHost(factory.Object);
        var vm = CreateViewModel(terminalHost);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        vm.ToggleBottomPanelCommand.Execute().Subscribe();

        service.Verify(s => s.Dispose(), Times.Never);
        Assert.Same(terminalVm, terminalHost.ActiveSession);
    }

    // ── Phase 5.4 M2: Townhall mirroring from SendAgentMessageAsync ───────────

    /// <summary>
    /// Creates a MainWindowViewModel with a real AgentPanelHost, real TownhallViewModel,
    /// and a mock IAgentExecutionCoordinator that appends user/assistant output on success.
    /// </summary>
    private static (MainWindowViewModel Vm, AgentPanelState Panel) CreateMirrorTestViewModel(
        string statusOnCompletion = "Idle",
        bool appendAssistantOutput = true)
    {
        // Create panel
        var agentHost = new AgentPanelHost();
        var panel = agentHost.CreatePanel("agent-1", "Test Agent", "avatar_test");

        // Mock coordinator that simulates a successful or failed send
        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<string, string, System.Threading.CancellationToken>((id, msg, ct) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null) return;
                p.OutputHistory.Add($"User: {msg}");
                if (appendAssistantOutput && statusOnCompletion != "Error")
                {
                    p.OutputHistory.Add("Assistant: Hello back");
                }
                else if (appendAssistantOutput && statusOnCompletion == "Error")
                {
                    p.OutputHistory.Add("Error: Request failed");
                }
                p.Status = statusOnCompletion;
                p.IsBusy = false;
            })
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Moq.Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser(agentHost);
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            mockCoordinator.Object, router, townhallViewModel, scViewModel, workspace);
        vm.Activate();
        return (vm, panel);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync mirrors the user request into Townhall
    /// before executing the coordinator.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_MirrorsUserRequestIntoTownhall()
    {
        var (vm, panel) = CreateMirrorTestViewModel();

        // Switch to a known channel so we have an active channel
        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello from test");

        // Townhall should have the user message (1 entry) plus the agent response (1 entry)
        Assert.Equal(beforeTownhallCount + 2, vm.TownhallViewModel.Messages.Count);

        // The first new entry should be the user message
        var userEntry = vm.TownhallViewModel.Messages[beforeTownhallCount];
        Assert.Equal(TownhallMessageKind.Chat, userEntry.Kind);
        Assert.Equal("Hello from test", userEntry.Content);
        Assert.Equal("user-1", userEntry.SenderId);
        Assert.Equal("User", userEntry.SenderName);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync mirrors the agent response into Townhall
    /// after a successful send.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_MirrorsAgentResponseIntoTownhall()
    {
        var (vm, panel) = CreateMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello");

        // Last entry should be the assistant response mirrored into Townhall
        var lastEntry = vm.TownhallViewModel.Messages[vm.TownhallViewModel.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.Chat, lastEntry.Kind);
        Assert.Contains("Hello back", lastEntry.Content);
        Assert.Equal("agent-1", lastEntry.SenderId);
        Assert.Equal("Test Agent", lastEntry.SenderName);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync mirrors an AgentError into Townhall
    /// when the panel ends in Error status.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_MirrorsErrorIntoTownhall()
    {
        var (vm, panel) = CreateMirrorTestViewModel(statusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello");

        // Last entry should be the error mirrored into Townhall
        var lastEntry = vm.TownhallViewModel.Messages[vm.TownhallViewModel.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.AgentError, lastEntry.Kind);
        Assert.Equal("agent-1", lastEntry.SenderId);
        Assert.Equal("Test Agent", lastEntry.SenderName);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync does not crash when the panel ID is unknown.
    /// The user message should still be mirrored, but no response/error entry.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_UnknownPanel_UserMessageStillMirrored()
    {
        var (vm, _) = CreateMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        // Use a panel ID that does not exist
        await vm.SendAgentMessageAsync("non-existent-panel", "Hello");

        // User message should have been mirrored (before the await),
        // but no response/error entry since the panel was not found.
        Assert.Equal(beforeCount + 1, vm.TownhallViewModel.Messages.Count);
        var userEntry = vm.TownhallViewModel.Messages[beforeCount];
        Assert.Equal(TownhallMessageKind.Chat, userEntry.Kind);
        Assert.Equal("Hello", userEntry.Content);
        Assert.Equal("user-1", userEntry.SenderId);
    }

    // ── Phase 5.4 M3: Alignment between panel-visible state and Townhall-visible state ──

    /// <summary>
    /// Verifies that after a successful send, the panel OutputHistory and Townhall messages
    /// contain matching content for both the user request and the agent response.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ContentMatchesPanelOutput()
    {
        var (vm, panel) = CreateMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeTownhallCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Hello from alignment test");

        // Panel should have user entry + assistant entry
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello from alignment test", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);

        // Townhall should have user entry + assistant entry
        Assert.Equal(beforeTownhallCount + 2, vm.TownhallViewModel.Messages.Count);

        // User entry in Townhall should match the user message (without "User: " prefix)
        var userEntry = vm.TownhallViewModel.Messages[beforeTownhallCount];
        Assert.Equal("Hello from alignment test", userEntry.Content);
        Assert.Equal("user-1", userEntry.SenderId);
        Assert.Equal(TownhallMessageKind.Chat, userEntry.Kind);

        // Agent entry in Townhall should match the assistant output (with "Assistant: " prefix)
        var agentEntry = vm.TownhallViewModel.Messages[beforeTownhallCount + 1];
        Assert.Equal("Assistant: Hello back", agentEntry.Content);
        Assert.Equal("agent-1", agentEntry.SenderId);
        Assert.Equal(TownhallMessageKind.Chat, agentEntry.Kind);
    }

    /// <summary>
    /// Verifies that after an error, the panel-visible error state is aligned with
    /// the Townhall-visible AgentError entry content.
    /// The realistic mock appends the "Error: ..." line when statusOnCompletion="Error".
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ErrorContentMatchesPanelOutput()
    {
        var (vm, panel) = CreateMirrorTestViewModel(statusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        await vm.SendAgentMessageAsync(panel.PanelId, "Trigger error");

        // Panel has user entry + realistic error entry from mock
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Trigger error", panel.OutputHistory[0]);
        Assert.Equal("Error: Request failed", panel.OutputHistory[1]);
        Assert.Equal("Error", panel.Status);

        // Townhall last entry should be an AgentError with the visible error output
        var lastEntry = vm.TownhallViewModel.Messages[vm.TownhallViewModel.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.AgentError, lastEntry.Kind);
        Assert.Equal("Error: Request failed", lastEntry.Content);
        Assert.Equal("agent-1", lastEntry.SenderId);
        Assert.Equal("Test Agent", lastEntry.SenderName);
    }

    /// <summary>
    /// Verifies that when appendAssistantOutput=false on error, the guard prevents
    /// mirroring a non-error line; no AgentError is added to Townhall.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_ErrorWithSingleOutput_UsesThatOutput()
    {
        var (vm, panel) = CreateMirrorTestViewModel(statusOnCompletion: "Error", appendAssistantOutput: false);

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;
        await vm.SendAgentMessageAsync(panel.PanelId, "Single output error");

        // Panel has only the user entry (appendAssistantOutput=false + status=Error)
        Assert.Single(panel.OutputHistory);
        Assert.Equal("User: Single output error", panel.OutputHistory[0]);
        Assert.Equal("Error", panel.Status);

        // Guard ensures no AgentError is mirrored when last line is not "Error: "
        Assert.Equal(beforeCount + 1, vm.TownhallViewModel.Messages.Count); // only the initial User mirror
    }

    /// <summary>
    /// Verifies that when the panel OutputHistory has entries but none start with
    /// "Assistant: ", the agent response is not mirrored into Townhall (guard clause).
    /// Only the user message should appear in Townhall.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_NonAssistantResponse_NotMirrored()
    {
        // Create a coordinator that appends output without "Assistant: " prefix
        var agentHost = new AgentPanelHost();
        var panel = agentHost.CreatePanel("agent-1", "Test Agent", "avatar_test");

        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<string, string, System.Threading.CancellationToken>((id, msg, ct) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null) return;
                p.OutputHistory.Add($"User: {msg}");
                p.OutputHistory.Add("Status: Completed");  // Not "Assistant: " prefixed
                p.Status = "Idle";
                p.IsBusy = false;
            })
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Moq.Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser(agentHost);
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            mockCoordinator.Object, router, townhallViewModel, scViewModel, workspace);
        vm.Activate();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Non-standard response");

        // Panel has user + non-assistant status entry
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Non-standard response", panel.OutputHistory[0]);
        Assert.Equal("Status: Completed", panel.OutputHistory[1]);

        // Only the user message should be mirrored (no "Assistant:" prefix match)
        Assert.Equal(beforeCount + 1, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("Non-standard response", vm.TownhallViewModel.Messages[beforeCount].Content);
    }

    /// <summary>
    /// Verifies that panel Status and Townhall message kind are aligned:
    /// Status="Error" → Townhall gets AgentError; Status="Idle" → Townhall gets Chat.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_PanelStatusAlignsWithTownhallKind()
    {
        var (vm, panel) = CreateMirrorTestViewModel(statusOnCompletion: "Idle");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        await vm.SendAgentMessageAsync(panel.PanelId, "Status alignment check");

        // After send, panel status should be Idle
        Assert.Equal("Idle", panel.Status);

        // Townhall last entry (agent response) should be Chat kind
        var lastEntry = vm.TownhallViewModel.Messages[vm.TownhallViewModel.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.Chat, lastEntry.Kind);
    }

    /// <summary>
    /// Verifies that SendAgentMessageAsync produces exactly two Townhall entries
    /// (user + response) in the correct order when successful.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_OrderIsUserThenResponse()
    {
        var (vm, panel) = CreateMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "Order check");

        // Exactly 2 new entries
        Assert.Equal(beforeCount + 2, vm.TownhallViewModel.Messages.Count);

        // First new entry is user request
        Assert.Equal("Order check", vm.TownhallViewModel.Messages[beforeCount].Content);
        Assert.Equal("user-1", vm.TownhallViewModel.Messages[beforeCount].SenderId);

        // Second new entry is agent response
        Assert.Equal("Assistant: Hello back", vm.TownhallViewModel.Messages[beforeCount + 1].Content);
        Assert.Equal("agent-1", vm.TownhallViewModel.Messages[beforeCount + 1].SenderId);
    }

    // ── Phase 6.1 M1: Consume RouteResult in SendAgentMessageAsync ──────────────

    /// <summary>
    /// Creates a MainWindowViewModel with a real AgentPanelHost holding TWO panels
    /// (source "Alpha" and target "Beta"), a real TownhallViewModel, and a mock
    /// coordinator that appends output to whichever panel the router targets.
    /// </summary>
    private static (MainWindowViewModel Vm, AgentPanelState Source, AgentPanelState Target) CreateTwoPanelMirrorTestViewModel(
        string targetStatusOnCompletion = "Idle",
        bool appendTargetOutput = true,
        Action<AgentPanelHost>? afterSend = null)
    {
        var agentHost = new AgentPanelHost();
        var source = agentHost.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var target = agentHost.CreatePanel("agent-2", "Beta", "avatar_beta");

        var mockCoordinator = new Moq.Mock<IAgentExecutionCoordinator>();
        mockCoordinator.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .Callback<string, string, System.Threading.CancellationToken>((id, msg, ct) =>
            {
                var p = agentHost.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null) return;
                p.OutputHistory.Add($"User: {msg}");
                if (appendTargetOutput && targetStatusOnCompletion != "Error")
                {
                    p.OutputHistory.Add("Assistant: Routed response");
                }
                else if (appendTargetOutput && targetStatusOnCompletion == "Error")
                {
                    p.OutputHistory.Add("Error: Something failed");
                }
                p.Status = targetStatusOnCompletion;
                p.IsBusy = false;
                afterSend?.Invoke(agentHost);
            })
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Moq.Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Moq.Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateScViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var parser = new MentionParser(agentHost);
        var router = new AgentRouter(parser, agentHost, mockCoordinator.Object);

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            mockCoordinator.Object, router, townhallViewModel, scViewModel, workspace);
        vm.Activate();
        return (vm, source, target);
    }

    /// <summary>
    /// Case A: unknown mention target surfaces as an AgentError under the source
    /// panel identity with a "Routing failed: Unknown target" message.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_UnknownTarget_MirrorsRoutingFailure()
    {
        var (vm, panel) = CreateMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(panel.PanelId, "@NonExistentAgent hello");

        // user message + routing-failure error entry
        Assert.Equal(beforeCount + 2, vm.TownhallViewModel.Messages.Count);

        var userEntry = vm.TownhallViewModel.Messages[beforeCount];
        Assert.Equal(TownhallMessageKind.Chat, userEntry.Kind);
        Assert.Equal("@NonExistentAgent hello", userEntry.Content);

        var errorEntry = vm.TownhallViewModel.Messages[beforeCount + 1];
        Assert.Equal(TownhallMessageKind.AgentError, errorEntry.Kind);
        Assert.Equal("Routing failed: Unknown target", errorEntry.Content);
        Assert.Equal(panel.AgentId, errorEntry.SenderId);
        Assert.Equal(panel.AgentName, errorEntry.SenderName);
    }

    /// <summary>
    /// Case A: multiple mentions surfaces as an AgentError under the source panel
    /// identity with a "Routing failed: Multiple mentions" message.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_MultipleMentions_MirrorsRoutingFailure()
    {
        var (vm, source, _) = CreateTwoPanelMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Alpha @Beta hello");

        Assert.Equal(beforeCount + 2, vm.TownhallViewModel.Messages.Count);

        var errorEntry = vm.TownhallViewModel.Messages[beforeCount + 1];
        Assert.Equal(TownhallMessageKind.AgentError, errorEntry.Kind);
        Assert.Equal("Routing failed: Multiple mentions", errorEntry.Content);
        Assert.Equal(source.AgentId, errorEntry.SenderId);
        Assert.Equal(source.AgentName, errorEntry.SenderName);
    }

    /// <summary>
    /// Case B: routed success mirrors the TARGET panel's assistant output into
    /// Townhall under the target panel identity.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_MirrorsTargetAssistantResponse()
    {
        var (vm, source, target) = CreateTwoPanelMirrorTestViewModel();

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Beta hello");

        Assert.Equal(beforeCount + 2, vm.TownhallViewModel.Messages.Count);

        var responseEntry = vm.TownhallViewModel.Messages[beforeCount + 1];
        Assert.Equal(TownhallMessageKind.Chat, responseEntry.Kind);
        Assert.Equal("Assistant: Routed response", responseEntry.Content);
        Assert.Equal(target.AgentId, responseEntry.SenderId);
        Assert.Equal(target.AgentName, responseEntry.SenderName);
    }

    /// <summary>
    /// Case B: routed success where the target panel ends in Error status mirrors
    /// an AgentError under the target panel identity.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_MirrorsTargetError()
    {
        var (vm, source, target) = CreateTwoPanelMirrorTestViewModel(targetStatusOnCompletion: "Error");

        var channelId = vm.TownhallViewModel.Channels[0].Id;
        vm.TownhallViewModel.SelectChannelCommand.Execute(channelId).Subscribe();

        var beforeCount = vm.TownhallViewModel.Messages.Count;

        await vm.SendAgentMessageAsync(source.PanelId, "@Beta hello");

        Assert.Equal(beforeCount + 2, vm.TownhallViewModel.Messages.Count);

        var errorEntry = vm.TownhallViewModel.Messages[beforeCount + 1];
        Assert.Equal(TownhallMessageKind.AgentError, errorEntry.Kind);
        Assert.Equal("Error: Something failed", errorEntry.Content);
        Assert.Equal(target.AgentId, errorEntry.SenderId);
        Assert.Equal(target.AgentName, errorEntry.SenderName);
    }

    /// <summary>
    /// Case B: routed success where the target panel has vanished before mirroring
    /// must not crash and must not add any entry beyond the user message.
    /// </summary>
    [Fact]
    public async Task SendAgentMessageAsync_RoutedSuccess_VanishedTargetPanel_NoExtraEntry()
    {
        AgentPanelState? target = null;
        // Remove the target panel DURING execution: routing still resolves the
        // mention (panel present at parse time), but the panel is gone by the time
        // the view model tries to mirror the routed output.
        var (vm, source, t) = CreateTwoPanelMirrorTestViewModel(
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

        // Only the user message; no crash and no mirrored target output.
        Assert.Equal(beforeCount + 1, vm.TownhallViewModel.Messages.Count);
        Assert.Equal("@Beta hello", vm.TownhallViewModel.Messages[beforeCount].Content);
        Assert.Equal(TownhallMessageKind.Chat, vm.TownhallViewModel.Messages[beforeCount].Kind);
    }

    [Fact]
    public void HideBottomPanel_HidesPanelWithoutDestroyingLastSession()
    {
        var service = new Mock<ITerminalService>();
        var terminalVm = new TerminalViewModel(service.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);
        var terminalHost = new TerminalHost(factory.Object);
        var vm = CreateViewModel(terminalHost);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.True(vm.IsBottomPanelVisible);

        vm.HideBottomPanelCommand.Execute().Subscribe();

        Assert.False(vm.IsBottomPanelVisible);
        service.Verify(s => s.Dispose(), Times.Never);
        Assert.Single(terminalHost.Tabs);
        Assert.Same(terminalVm, terminalHost.ActiveSession);
    }
}
