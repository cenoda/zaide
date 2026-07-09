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
    private string? _statusText = "Open a folder to begin";
    private CompositeDisposable? _disposables;
    private LeftPanelMode _leftPanelMode = LeftPanelMode.Explorer;
    private bool _isExplorerMode = true;
    private bool _isSourceControlMode;
    private readonly Workspace _workspace;


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
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToExplorerCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSourceControlCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }
    public EditorTabViewModel EditorTabs { get; }
    public ITerminalHost TerminalHost { get; }
    public IAgentPanelHost AgentPanelHost { get; }
    public IAgentExecutionCoordinator AgentExecutionCoordinator { get; }
    public IAgentRouter AgentRouter { get; }
    public TownhallViewModel TownhallViewModel { get; }
    public SourceControlViewModel SourceControlViewModel { get; }

    /// <summary>
    /// Project name for the status bar. Derived from Workspace.
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
                               Workspace workspace)
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
        WorkspaceProjectName = workspace.ProjectName;
        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);
        HideBottomPanelCommand = ReactiveCommand.Create(HideBottomPanel);
        SaveActiveTabCommand = ReactiveCommand.CreateFromTask(SaveActiveTabAsync);
        PickFolder = new Interaction<Unit, string?>();
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
        // file-tree header.
        _disposables.Add(
            this.WhenAnyValue(x => x.FileTreeViewModel.RootPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Subscribe(path =>
                {
                    _workspace.SetProjectFromPath(path);
                    WorkspaceProjectName = _workspace.ProjectName;
                    SourceControlViewModel.RefreshCommand.Execute(Unit.Default).Subscribe();
                }));

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
        var routeResult = await AgentRouter.RouteAndExecuteAsync(panelId, userMessage, ct).ConfigureAwait(false);

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
