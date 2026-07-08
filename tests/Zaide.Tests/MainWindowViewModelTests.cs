using System;
using System.IO;
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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, new AgentPanelHost(), townhallViewModel, scViewModel, workspace);
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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost, new AgentPanelHost(), townhallViewModel, scViewModel, workspace);
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
        var vm = new MainWindowViewModel(fileTreeViewModel, editorTabs, terminalHost2, new AgentPanelHost(), townhallViewModel2, scViewModel2, workspace2);
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
}
