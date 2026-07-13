using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Defines which panel is shown in the left content slot.
/// </summary>
public enum LeftPanelMode
{
    Explorer,
    SourceControl
}

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private bool _isBottomPanelVisible;
    private string? _statusText;
    private CompositeDisposable? _disposables;
    private LeftPanelMode _leftPanelMode = LeftPanelMode.Explorer;
    private bool _isExplorerMode = true;
    private bool _isSourceControlMode;
    private ProjectContext _currentProjectContext = null!;
    private readonly Workspace _workspace;
    private readonly IProjectContextService _projectContextService;

    /// <summary>
    /// Scheduler for the <see cref="IProjectContextService.WhenChanged"/>
    /// subscription. Exposed as internal so tests can substitute a deterministic
    /// scheduler without injecting a new constructor parameter.
    /// Defaults to <see cref="AvaloniaScheduler.Instance"/>.
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

    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> HideBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveActiveTabCommand { get; }
    public Interaction<Unit, string?> PickFolder { get; }
    public Interaction<Unit, bool> ShowSettings { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToExplorerCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSourceControlCommand { get; }

    /// <summary>
    /// M3 (Phase 8.1.3): Closes the open folder. Enabled only while a folder is open.
    /// Not registered in a command registry — local to this ViewModel.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CloseFolderCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }
    public EditorTabViewModel EditorTabs { get; }
    public ITerminalHost TerminalHost { get; }
    public IAgentPanelHost AgentPanelHost { get; }
    public IAgentExecutionCoordinator AgentExecutionCoordinator { get; }
    public IAgentRouter AgentRouter { get; }
    public TownhallViewModel TownhallViewModel { get; }
    public SourceControlViewModel SourceControlViewModel { get; }

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
    /// Legacy consumers may continue using this; new consumers should prefer
    /// <see cref="CurrentProjectContext"/>.
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
                                IAgentExecutionCoordinator agentExecutionCoordinator,
                                IAgentRouter agentRouter,
                                TownhallViewModel townhallViewModel,
                                SourceControlViewModel sourceControlViewModel,
                                Workspace workspace,
                                IProjectContextService projectContextService,
                                ICommandRegistry? commandRegistry = null)
    {
        FileTreeViewModel = fileTreeViewModel;
        EditorTabs = editorTabViewModel;
        TerminalHost = terminalHost;
        AgentPanelHost = agentPanelHost;
        AgentExecutionCoordinator = agentExecutionCoordinator;
        AgentRouter = agentRouter;
        TownhallViewModel = townhallViewModel;
        SourceControlViewModel = sourceControlViewModel;
        _workspace = workspace;
        _projectContextService = projectContextService;
        CurrentProjectContext = projectContextService.Current;
        WorkspaceProjectName = workspace.ProjectName;
        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);
        HideBottomPanelCommand = ReactiveCommand.Create(HideBottomPanel);
        SaveActiveTabCommand = ReactiveCommand.CreateFromTask(SaveActiveTabAsync);
        PickFolder = new Interaction<Unit, string?>();
        ShowSettings = new Interaction<Unit, bool>();
        OpenFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await PickFolder.Handle(Unit.Default);
            if (path is not null)
            {
                // Delegate to the file tree. Syncing the shared workspace and
                // Source Control is driven by the FileTreeViewModel.RootPath
                // subscription in Activate() so every open-folder entry point
                // (Ctrl+O here and the file-tree "Open Folder..." header) stays
                // consistent and truthful.
                await FileTreeViewModel.OpenFolderCommand.Execute(path);
            }
        });
        SwitchToExplorerCommand = ReactiveCommand.Create(() => { LeftPanelMode = LeftPanelMode.Explorer; });
        SwitchToSourceControlCommand = ReactiveCommand.Create(() => { LeftPanelMode = LeftPanelMode.SourceControl; });

        // M3 (Phase 8.1.3): Close-folder command. Enabled only while a folder is open.
        // Calls SetRootPath(null) directly. The CloseFolderRequested interaction
        // (bridged in Activate()) is the view→ViewModel entry point that invokes this command.
        var canCloseFolder = this.WhenAnyValue(x => x.FileTreeViewModel.RootPath)
            .Select(path => path is not null);
        CloseFolderCommand = ReactiveCommand.Create(() =>
        {
            FileTreeViewModel.SetRootPath(null);
        }, canCloseFolder);

        // Phase 8.2 M8a: register the canonical window commands with stable IDs
        // (D6a). Registration happens after every ReactiveCommand property above
        // is initialized. Production DI always supplies the singleton registry;
        // the optional parameter lets tests opt in without a second path.
        commandRegistry?.Register(new CommandDescriptor(
            "file.save", "Save", "File", new[] { "Ctrl+S" }, SaveActiveTabCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "workspace.openFolder", "Open Folder", "Workspace", new[] { "Ctrl+O" }, OpenFolderCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "workspace.closeFolder", "Close Folder", "Workspace", Array.Empty<string>(), CloseFolderCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "view.toggleBottomPanel", "Toggle Bottom Panel", "View", new[] { "Ctrl+Oem3", "Ctrl+J" }, ToggleBottomPanelCommand));
    }

    /// <summary>
    /// Starts reactive subscriptions. Called by the View during activation.
    /// Safe to call multiple times — re-entrant guard prevents duplicates.
    /// </summary>
    public void Activate()
    {
        if (_disposables is not null) return;

        _disposables = new CompositeDisposable();

        // Keep the shared workspace + Source Control in sync with whichever
        // folder is loaded in the file tree, regardless of the open-folder entry
        // point (Ctrl+O via OpenFolderCommand or the file-tree "Open Folder..."
        // header, which invokes FileTreeViewModel.OpenFolderCommand directly).
        // RootPath is the single post-validation truth for the loaded folder, so
        // reacting to it prevents "No repository" when a repo is opened from the
        // file-tree header. M3: the null filter is removed so close transitions
        // also flow through SetProjectFromPath(null) and Source Control refresh.
        _disposables.Add(
            this.WhenAnyValue(x => x.FileTreeViewModel.RootPath)
                .Subscribe(path =>
                {
                    _workspace.SetProjectFromPath(path);
                    WorkspaceProjectName = _workspace.ProjectName;
                    SourceControlViewModel.RefreshCommand.Execute(Unit.Default).Subscribe();
                }));

        // M3 (Phase 8.1.3): Bridge CloseFolderRequested interaction.
        // Executes CloseFolderCommand only when it can execute (folder is open).
        // Always completes the interaction output, including no-folder cases.
        // The handler is synchronous (not async void) so the interaction output
        // is set after the command body completes — callers that subscribe to
        // Handle() observe the post-command state immediately.
        _disposables.Add(
            FileTreeViewModel.CloseFolderRequested.RegisterHandler(interaction =>
            {
                if (FileTreeViewModel.RootPath is not null)
                {
                    CloseFolderCommand.Execute().GetAwaiter().GetResult();
                }
                interaction.SetOutput(Unit.Default);
            }));

        // M4: Subscribe to authoritative project-context snapshots on the UI thread.
        _disposables.Add(
            _projectContextService.WhenChanged
                .ObserveOn(ProjectContextScheduler)
                .Subscribe(ctx => CurrentProjectContext = ctx));

        // Phase 9 M6: Clear status text on tab switch to prevent stale messages
        // from old tabs leaking into the current view. Each new event from the
        // active tab will set its own fresh status.
        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.ActiveTab)
                .Subscribe(_ => StatusText = null));

        // Phase 9 M6: Route fold status messages from the tab manager to the
        // status bar. Cleared on tab switch via the ActiveTab subscription above.
        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.FoldStatusMessage)
                .Where(msg => msg is not null)
                .Subscribe(msg => StatusText = msg));

        // Surface save errors from the tab manager
        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.LastSaveError)
                .Where(msg => msg is not null)
                .Subscribe(msg => StatusText = $"Save failed: {msg}"));

        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.LastOpenError)
                .Where(msg => msg is not null)
                .Subscribe(msg => StatusText = $"Open failed: {msg}"));

        _disposables.Add(
            TerminalHost.StartupError
                .Where(err => err is not null)
                .Subscribe(err => StatusText = $"Terminal: {err}"));

        // Subscribe to OpenFileRequested (published by RequestOpenFileCommand).
        // Uses the FileTreeNode payload directly — no dependency on SelectedFile.
        _disposables.Add(
            FileTreeViewModel.OpenFileRequested.Subscribe(node =>
            {
                var path = node.FullPath;
                var unsupported = SupportedFileTypes.GetUnsupportedMessage(path);

                if (unsupported is null)
                {
                    EditorTabs.OpenFileCommand.Execute(path).Subscribe(result =>
                    {
                        if (result)
                            StatusText = $"Opened: {node.Name}";
                    });
                }
                else
                {
                    StatusText = unsupported;
                }
            }));
    }

    /// <summary>
    /// Thin delegating seam for the agent panel send flow.
    /// Delegates routing decisions and orchestration to <see cref="IAgentRouter"/>.
    /// Router owns mention parsing, resolution, direct-vs-routed decision, and
    /// coordination. MainWindowViewModel remains composition/delegation only.
    /// </summary>
    public async Task SendAgentMessageAsync(string panelId, string userMessage, CancellationToken ct = default)
    {
        // Mirror the user request into Townhall before routing (preserves current truthful behavior).
        TownhallViewModel.AddMirroredActivity(
            kind: TownhallMessageKind.Chat,
            content: userMessage,
            senderId: "user-1",
            senderName: "User");

        // Delegate entirely to the routing orchestration seam (M3).
        // NOTE: Do NOT use ConfigureAwait(false) here. The continuation reads
        // AgentPanelState (OutputHistory, Status) and calls
        // TownhallViewModel.AddMirroredActivity() which modifies
        // ObservableCollection<TownhallMessage> — both require the Avalonia UI
        // thread. AgentRouter and AgentExecutionCoordinator also preserve the
        // captured SynchronizationContext internally.
        var routeResult = await AgentRouter.RouteAndExecuteAsync(panelId, userMessage, ct);

        // M1: consume the routing outcome so routed flows and routing failures
        // become visible in Townhall (previously the result was captured but unread).
        var sourcePanel = AgentPanelHost.Panels.FirstOrDefault(p => p.PanelId == panelId);

        // Case A: parse/routing failure. Surface as an AgentError under the source
        // panel identity. If the source panel is gone, there is nothing to attribute to.
        if (!routeResult.Success)
        {
            if (sourcePanel is null)
                return;

            TownhallViewModel.AddMirroredActivity(
                kind: TownhallMessageKind.AgentError,
                content: $"Routing failed: {routeResult.FailureReason}",
                senderId: sourcePanel.AgentId,
                senderName: sourcePanel.AgentName);
            return;
        }

        // Choose which panel's output to mirror:
        //   Case B (routed): the resolved target panel.
        //   Case C (direct send): the source panel (unchanged existing behavior).
        var request = routeResult.Request;
        var panel = request is not null && !request.IsDirectSend
            ? AgentPanelHost.Panels.FirstOrDefault(p => p.AgentName == request.TargetAgentName)
            : sourcePanel;

        if (panel is null)
            return;

        if (panel.Status == "Error")
        {
            var lastOutput = panel.OutputHistory.Count > 0 ? panel.OutputHistory[^1] : null;
            if (lastOutput is not null && lastOutput.StartsWith("Error: "))
            {
                TownhallViewModel.AddMirroredActivity(
                    kind: TownhallMessageKind.AgentError,
                    content: lastOutput,
                    senderId: panel.AgentId,
                    senderName: panel.AgentName);
            }
        }
        else
        {
            var lastOutput = panel.OutputHistory.Count > 0
                ? panel.OutputHistory[^1]
                : null;
            if (lastOutput is not null && lastOutput.StartsWith("Assistant: "))
            {
                TownhallViewModel.AddMirroredActivity(
                    kind: TownhallMessageKind.Chat,
                    content: lastOutput,
                    senderId: panel.AgentId,
                    senderName: panel.AgentName);
            }
        }
    }

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }

    private void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }

    private void HideBottomPanel()
    {
        IsBottomPanelVisible = false;
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
