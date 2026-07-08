using System;
using System.IO;
using System.Linq;
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
        var scState = new SourceControlState();
        var scViewModel = new SourceControlViewModel(scState);
        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, new AgentPanelHost(), coordinator, townhallViewModel, scViewModel, workspace);
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
        var scState = new SourceControlState();
        var scViewModel = new SourceControlViewModel(scState);
        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
        var coordinator = CreateMockCoordinator().Object;
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, new AgentPanelHost(), coordinator, townhallViewModel, scViewModel, workspace);
        vm.Activate();
        return vm;
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

        var scState2 = new SourceControlState();
        var scViewModel2 = new SourceControlViewModel(scState2);
        var workspace2 = sp.GetRequiredService<Workspace>();
        var coordinator2 = CreateMockCoordinator().Object;
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost2, new AgentPanelHost(), coordinator2, townhallViewModel2, scViewModel2, workspace2);
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
        var scState = new SourceControlState();
        var scViewModel = new SourceControlViewModel(scState);
        var workspace = sp.GetRequiredService<Workspace>();

        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, agentHost,
            mockCoordinator.Object, townhallViewModel, scViewModel, workspace);
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
