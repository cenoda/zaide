using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.App.Composition;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;

namespace Zaide.App.Shell;
/// <summary>
/// Defines which panel is shown in the left content slot.
/// </summary>
public enum LeftPanelMode
{
    Explorer,
    SourceControl
}

/// <summary>
/// Defines which surface is shown in the bottom panel content area.
/// </summary>
public enum BottomPanelMode
{
    Terminal,
    Problems,
    Output,
    TestResults,
    Debug,
}

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private bool _isBottomPanelVisible;
    private string? _statusText;
    private CompositeDisposable? _disposables;
    private LeftPanelMode _leftPanelMode = LeftPanelMode.Explorer;
    private BottomPanelMode _bottomPanelMode = BottomPanelMode.Terminal;
    private bool _isExplorerMode = true;
    private bool _isSourceControlMode;
    private bool _isTerminalBottomMode = true;
    private bool _isProblemsBottomMode;
    private bool _isOutputBottomMode;
    private bool _isTestResultsBottomMode;
    private bool _isDebugBottomMode;
    private bool _isSettingsOpen;
    private ProjectContext _currentProjectContext = null!;
    private readonly Workspace _workspace;
    private readonly IProjectContextService _projectContextService;
    private readonly AgentTownhallMirrorCoordinator _agentTownhallMirror;
    private readonly ShellPanelNavigation _panelNavigation;
    private readonly MainWindowActivationHost _activationHost;

    /// <summary>
    /// Scheduler for project-context <c>WhenChanged</c>. Internal so tests can
    /// substitute a deterministic scheduler without a new ctor parameter.
    /// </summary>
    internal System.Reactive.Concurrency.IScheduler ProjectContextScheduler { get; set; }
        = AvaloniaScheduler.Instance;

    public bool IsBottomPanelVisible
    {
        get => _isBottomPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelVisible, value);
    }

    public string? StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public LeftPanelMode LeftPanelMode
    {
        get => _leftPanelMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _leftPanelMode, value);
            IsExplorerMode = value == LeftPanelMode.Explorer;
            IsSourceControlMode = value == LeftPanelMode.SourceControl;
        }
    }

    public bool IsExplorerMode
    {
        get => _isExplorerMode;
        private set => this.RaiseAndSetIfChanged(ref _isExplorerMode, value);
    }
    public bool IsSourceControlMode
    {
        get => _isSourceControlMode;
        private set => this.RaiseAndSetIfChanged(ref _isSourceControlMode, value);
    }

    public BottomPanelMode BottomPanelMode
    {
        get => _bottomPanelMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _bottomPanelMode, value);
            IsTerminalBottomMode = value == BottomPanelMode.Terminal;
            IsProblemsBottomMode = value == BottomPanelMode.Problems;
            IsOutputBottomMode = value == BottomPanelMode.Output;
            IsTestResultsBottomMode = value == BottomPanelMode.TestResults;
            IsDebugBottomMode = value == BottomPanelMode.Debug;
        }
    }

    public bool IsTerminalBottomMode
    {
        get => _isTerminalBottomMode;
        private set => this.RaiseAndSetIfChanged(ref _isTerminalBottomMode, value);
    }
    public bool IsProblemsBottomMode
    {
        get => _isProblemsBottomMode;
        private set => this.RaiseAndSetIfChanged(ref _isProblemsBottomMode, value);
    }
    public bool IsOutputBottomMode
    {
        get => _isOutputBottomMode;
        private set => this.RaiseAndSetIfChanged(ref _isOutputBottomMode, value);
    }
    public bool IsTestResultsBottomMode
    {
        get => _isTestResultsBottomMode;
        private set => this.RaiseAndSetIfChanged(ref _isTestResultsBottomMode, value);
    }
    public bool IsDebugBottomMode
    {
        get => _isDebugBottomMode;
        private set => this.RaiseAndSetIfChanged(ref _isDebugBottomMode, value);
    }

    /// <summary>True while the full-window settings overlay is visible.</summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        internal set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> HideBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveActiveTabCommand { get; }
    public Interaction<Unit, string?> PickFolder { get; }
    public Interaction<Unit, bool> ShowSettings { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToExplorerCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSourceControlCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToTerminalBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToProblemsBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToOutputBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToTestResultsBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToDebugBottomCommand { get; }

    /// <summary>
    /// M3 (Phase 8.1.3): Closes the open folder. Enabled only while a folder is open.
    /// Not registered in a command registry — local to this ViewModel.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CloseFolderCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }
    public EditorTabViewModel EditorTabs { get; }
    public ITerminalHost TerminalHost { get; }
    public IAgentPanelHost AgentPanelHost { get; }
    public TownhallViewModel TownhallViewModel { get; }
    public SourceControlViewModel SourceControlViewModel { get; }
    public ProblemsViewModel ProblemsViewModel { get; }
    public ProjectWorkflowViewModel ProjectWorkflowViewModel { get; }
    public TestResultsViewModel TestResultsViewModel { get; }
    public DebugSessionViewModel DebugSessionViewModel { get; }
    public DebugPanelViewModel DebugPanelViewModel { get; }
    public EditorBreakpointViewModel EditorBreakpointViewModel { get; }
    public DebugCurrentLocationViewModel? DebugCurrentLocationViewModel { get; }

    /// <summary>
    /// M4: Authoritative UI-thread projection of the current project-context
    /// snapshot. Updated by the <see cref="Activate"/> subscription to
    /// <see cref="IProjectContextService.WhenChanged"/>.
    /// </summary>
    public ProjectContext CurrentProjectContext
    {
        get => _currentProjectContext;
        private set => this.RaiseAndSetIfChanged(ref _currentProjectContext, value);
    }

    /// <summary>
    /// Project name for the status bar. Derived from Workspace.
    /// Prefer <see cref="CurrentProjectContext"/> for new consumers.
    /// </summary>
    private string? _workspaceProjectName;
    public string? WorkspaceProjectName
    {
        get => _workspaceProjectName;
        set => this.RaiseAndSetIfChanged(ref _workspaceProjectName, value);
    }

    public MainWindowViewModel(FileTreeViewModel fileTreeViewModel,
                                EditorTabViewModel editorTabViewModel,
                                ITerminalHost terminalHost,
                                IAgentPanelHost agentPanelHost,
                                IAgentRouter agentRouter,
                                TownhallViewModel townhallViewModel,
                                SourceControlViewModel sourceControlViewModel,
                                ProblemsViewModel problemsViewModel,
                                ProjectWorkflowViewModel projectWorkflowViewModel,
                                TestResultsViewModel testResultsViewModel,
                                DebugSessionViewModel debugSessionViewModel,
                                DebugPanelViewModel debugPanelViewModel,
                                EditorBreakpointViewModel editorBreakpointViewModel,
                                Workspace workspace,
                                IProjectContextService projectContextService,
                                ICommandRegistry? commandRegistry = null,
                                DebugCurrentLocationViewModel? debugCurrentLocationViewModel = null)
    {
        FileTreeViewModel = fileTreeViewModel;
        EditorTabs = editorTabViewModel;
        TerminalHost = terminalHost;
        AgentPanelHost = agentPanelHost;
        TownhallViewModel = townhallViewModel;
        _agentTownhallMirror = new AgentTownhallMirrorCoordinator(
            agentRouter,
            agentPanelHost,
            townhallViewModel);
        SourceControlViewModel = sourceControlViewModel;
        ProblemsViewModel = problemsViewModel ?? throw new ArgumentNullException(nameof(problemsViewModel));
        ProjectWorkflowViewModel = projectWorkflowViewModel ?? throw new ArgumentNullException(nameof(projectWorkflowViewModel));
        TestResultsViewModel = testResultsViewModel ?? throw new ArgumentNullException(nameof(testResultsViewModel));
        DebugSessionViewModel = debugSessionViewModel ?? throw new ArgumentNullException(nameof(debugSessionViewModel));
        DebugPanelViewModel = debugPanelViewModel ?? throw new ArgumentNullException(nameof(debugPanelViewModel));
        EditorBreakpointViewModel = editorBreakpointViewModel ?? throw new ArgumentNullException(nameof(editorBreakpointViewModel));
        DebugCurrentLocationViewModel = debugCurrentLocationViewModel;

        // Phase 11 F9: save all dirty editor tabs before Build / Run / Test.
        ProjectWorkflowViewModel.SaveAllDirtyTabsAsync = () => editorTabViewModel.SaveAllDirtyTabsAsync();
        DebugSessionViewModel.SaveAllDirtyTabsAsync = () => editorTabViewModel.SaveAllDirtyTabsAsync();
        _workspace = workspace;
        _projectContextService = projectContextService;
        CurrentProjectContext = projectContextService.Current;
        WorkspaceProjectName = workspace.ProjectName;
        _panelNavigation = new ShellPanelNavigation(
            setLeft: mode => LeftPanelMode = mode,
            setBottom: mode => BottomPanelMode = mode,
            setBottomVisible: visible => IsBottomPanelVisible = visible,
            getBottomVisible: () => IsBottomPanelVisible);
        SwitchToExplorerCommand = _panelNavigation.SwitchToExplorerCommand;
        SwitchToSourceControlCommand = _panelNavigation.SwitchToSourceControlCommand;
        SwitchToTerminalBottomCommand = _panelNavigation.SwitchToTerminalBottomCommand;
        SwitchToProblemsBottomCommand = _panelNavigation.SwitchToProblemsBottomCommand;
        SwitchToOutputBottomCommand = _panelNavigation.SwitchToOutputBottomCommand;
        SwitchToTestResultsBottomCommand = _panelNavigation.SwitchToTestResultsBottomCommand;
        SwitchToDebugBottomCommand = _panelNavigation.SwitchToDebugBottomCommand;
        ToggleBottomPanelCommand = _panelNavigation.ToggleBottomPanelCommand;
        HideBottomPanelCommand = _panelNavigation.HideBottomPanelCommand;
        SaveActiveTabCommand = ReactiveCommand.CreateFromTask(SaveActiveTabAsync);
        PickFolder = new Interaction<Unit, string?>();
        ShowSettings = new Interaction<Unit, bool>();
        // Open-folder: delegate to the file tree. Workspace/SC sync is driven by
        // FileTreeViewModel.RootPath in Activate() for every open-folder entry point.
        OpenFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await PickFolder.Handle(Unit.Default);
            if (path is not null)
            {
                await FileTreeViewModel.OpenFolderCommand.Execute(path);
            }
        });

        // M3 (Phase 8.1.3): enabled only while a folder is open; bridged from
        // CloseFolderRequested in Activate().
        var canCloseFolder = this.WhenAnyValue(x => x.FileTreeViewModel.RootPath)
            .Select(path => path is not null);
        CloseFolderCommand = ReactiveCommand.Create(() =>
        {
            FileTreeViewModel.SetRootPath(null);
        }, canCloseFolder);

        // Phase 8.2 M8a: register canonical window commands (D6a) after all
        // ReactiveCommand properties are initialized. Tests may omit the registry.
        commandRegistry?.Register(new CommandDescriptor(
            "file.save", "Save", "File", new[] { "Ctrl+S" }, SaveActiveTabCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "workspace.openFolder", "Open Folder", "Workspace", new[] { "Ctrl+O" }, OpenFolderCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "workspace.closeFolder", "Close Folder", "Workspace", Array.Empty<string>(), CloseFolderCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "view.toggleBottomPanel", "Toggle Bottom Panel", "View", new[] { "Ctrl+Oem3", "Ctrl+J" }, ToggleBottomPanelCommand));

        // Construct after CloseFolderCommand and all Activate dependencies exist.
        // Scheduler is resolved via getter at Activate time so tests can substitute
        // ProjectContextScheduler after construction without a stale capture.
        _activationHost = new MainWindowActivationHost(
            ProblemsViewModel,
            ProjectWorkflowViewModel,
            DebugSessionViewModel,
            DebugPanelViewModel,
            EditorBreakpointViewModel,
            DebugCurrentLocationViewModel,
            TestResultsViewModel,
            FileTreeViewModel,
            SourceControlViewModel,
            EditorTabs,
            TerminalHost,
            workspace,
            projectContextService,
            () => ProjectContextScheduler,
            CloseFolderCommand,
            mode => BottomPanelMode = mode,
            visible => IsBottomPanelVisible = visible,
            text => StatusText = text,
            ctx => CurrentProjectContext = ctx,
            name => WorkspaceProjectName = name);
    }

    /// <summary>
    /// Starts reactive subscriptions. Called by the View during activation.
    /// Safe to call multiple times — re-entrant guard prevents duplicates.
    /// </summary>
    public void Activate()
    {
        if (_disposables is not null)
            return;

        _disposables = new CompositeDisposable();
        _activationHost.Activate(_disposables);
    }

    /// <summary>
    /// Forwards agent send to <see cref="AgentTownhallMirrorCoordinator"/>.
    /// Public name/signature unchanged for the view.
    /// </summary>
    public Task SendAgentMessageAsync(
        string panelId,
        string userMessage,
        CancellationToken ct = default) =>
        _agentTownhallMirror.SendAsync(panelId, userMessage, ct);

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }

    private async Task SaveActiveTabAsync()
    {
        var activeTab = EditorTabs.ActiveTab;
        if (activeTab is null)
            return;

        var saved = await activeTab.SaveCommand.Execute();

        // Phase 9 M6: stale-state check — the user may have switched tabs (or
        // closed the tab) while the save was in flight. Only surface the result
        // if the tab is still the active one.
        if (!ReferenceEquals(activeTab, EditorTabs.ActiveTab))
            return;

        if (saved)
        {
            StatusText = $"Saved: {activeTab.FileName}";
            return;
        }

        StatusText = activeTab.LastSaveError is { Length: > 0 } error
            ? $"Save failed: {error}"
            : $"Save failed: {activeTab.FileName}";
    }
}
