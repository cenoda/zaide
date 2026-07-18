using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.App.Shell;

/// <summary>
/// Owns main-window activation side-effects: feature ViewModel activation,
/// show-panel routing, workspace/source-control sync, project-context projection,
/// status-text routing, and open-file handling. Constructed inside
/// <see cref="MainWindowViewModel"/>; not DI-registered. Mode/status/context
/// mutation goes through injected delegates so MWVM retains notification ownership.
/// </summary>
internal sealed class MainWindowActivationHost
{
    private readonly ProblemsViewModel _problemsViewModel;
    private readonly ProjectWorkflowViewModel _projectWorkflowViewModel;
    private readonly DebugSessionViewModel _debugSessionViewModel;
    private readonly DebugPanelViewModel _debugPanelViewModel;
    private readonly EditorBreakpointViewModel _editorBreakpointViewModel;
    private readonly DebugCurrentLocationViewModel? _debugCurrentLocationViewModel;
    private readonly TestResultsViewModel _testResultsViewModel;
    private readonly FileTreeViewModel _fileTreeViewModel;
    private readonly SourceControlViewModel _sourceControlViewModel;
    private readonly EditorTabViewModel _editorTabs;
    private readonly ITerminalHost _terminalHost;
    private readonly Workspace _workspace;
    private readonly IProjectContextService _projectContextService;
    private readonly Func<IScheduler> _getProjectContextScheduler;
    private readonly ReactiveCommand<Unit, Unit> _closeFolderCommand;
    private readonly Action<BottomPanelMode> _setBottomPanelMode;
    private readonly Action<bool> _setIsBottomPanelVisible;
    private readonly Action<string?> _setStatusText;
    private readonly Action<ProjectContext> _setCurrentProjectContext;
    private readonly Action<string?> _setWorkspaceProjectName;

    public MainWindowActivationHost(
        ProblemsViewModel problemsViewModel,
        ProjectWorkflowViewModel projectWorkflowViewModel,
        DebugSessionViewModel debugSessionViewModel,
        DebugPanelViewModel debugPanelViewModel,
        EditorBreakpointViewModel editorBreakpointViewModel,
        DebugCurrentLocationViewModel? debugCurrentLocationViewModel,
        TestResultsViewModel testResultsViewModel,
        FileTreeViewModel fileTreeViewModel,
        SourceControlViewModel sourceControlViewModel,
        EditorTabViewModel editorTabs,
        ITerminalHost terminalHost,
        Workspace workspace,
        IProjectContextService projectContextService,
        Func<IScheduler> getProjectContextScheduler,
        ReactiveCommand<Unit, Unit> closeFolderCommand,
        Action<BottomPanelMode> setBottomPanelMode,
        Action<bool> setIsBottomPanelVisible,
        Action<string?> setStatusText,
        Action<ProjectContext> setCurrentProjectContext,
        Action<string?> setWorkspaceProjectName)
    {
        _problemsViewModel = problemsViewModel ?? throw new ArgumentNullException(nameof(problemsViewModel));
        _projectWorkflowViewModel = projectWorkflowViewModel ?? throw new ArgumentNullException(nameof(projectWorkflowViewModel));
        _debugSessionViewModel = debugSessionViewModel ?? throw new ArgumentNullException(nameof(debugSessionViewModel));
        _debugPanelViewModel = debugPanelViewModel ?? throw new ArgumentNullException(nameof(debugPanelViewModel));
        _editorBreakpointViewModel = editorBreakpointViewModel ?? throw new ArgumentNullException(nameof(editorBreakpointViewModel));
        _debugCurrentLocationViewModel = debugCurrentLocationViewModel;
        _testResultsViewModel = testResultsViewModel ?? throw new ArgumentNullException(nameof(testResultsViewModel));
        _fileTreeViewModel = fileTreeViewModel ?? throw new ArgumentNullException(nameof(fileTreeViewModel));
        _sourceControlViewModel = sourceControlViewModel ?? throw new ArgumentNullException(nameof(sourceControlViewModel));
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
        _terminalHost = terminalHost ?? throw new ArgumentNullException(nameof(terminalHost));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _projectContextService = projectContextService ?? throw new ArgumentNullException(nameof(projectContextService));
        _getProjectContextScheduler = getProjectContextScheduler ?? throw new ArgumentNullException(nameof(getProjectContextScheduler));
        _closeFolderCommand = closeFolderCommand ?? throw new ArgumentNullException(nameof(closeFolderCommand));
        _setBottomPanelMode = setBottomPanelMode ?? throw new ArgumentNullException(nameof(setBottomPanelMode));
        _setIsBottomPanelVisible = setIsBottomPanelVisible ?? throw new ArgumentNullException(nameof(setIsBottomPanelVisible));
        _setStatusText = setStatusText ?? throw new ArgumentNullException(nameof(setStatusText));
        _setCurrentProjectContext = setCurrentProjectContext ?? throw new ArgumentNullException(nameof(setCurrentProjectContext));
        _setWorkspaceProjectName = setWorkspaceProjectName ?? throw new ArgumentNullException(nameof(setWorkspaceProjectName));
    }

    /// <summary>
    /// Starts reactive subscriptions and feature activations. Caller owns
    /// idempotence and the <paramref name="disposables"/> lifetime.
    /// </summary>
    public void Activate(CompositeDisposable disposables)
    {
        ArgumentNullException.ThrowIfNull(disposables);

        // Phase 10 M3: start Problems projection once the window is active.
        _problemsViewModel.Activate();
        disposables.Add(_problemsViewModel);

        // Phase 11 M2: structured output projection and show-on-build affordance.
        _projectWorkflowViewModel.Activate();
        disposables.Add(_projectWorkflowViewModel);

        // Phase 12 M3a: debug session command projection. Singleton dispose is owned
        // by App.DisposeServicesOnExit after IDebugSessionService (Contract 3).
        _debugSessionViewModel.Activate();

        // Phase 12 M4: debug console and call-stack shell projection.
        _debugPanelViewModel.Activate();
        disposables.Add(
            _debugPanelViewModel.WhenShowDebugRequested
                .Subscribe(_ =>
                {
                    _setBottomPanelMode(BottomPanelMode.Debug);
                    _setIsBottomPanelVisible(true);
                }));

        // Phase 12 M3b: editor breakpoint projection and F9 command.
        _editorBreakpointViewModel.Activate();

        // Phase 12 M5: selected-frame current execution location projection.
        if (_debugCurrentLocationViewModel is not null)
            _debugCurrentLocationViewModel.Activate();
        disposables.Add(
            _projectWorkflowViewModel.WhenShowOutputRequested
                .Subscribe(_ =>
                {
                    _setBottomPanelMode(BottomPanelMode.Output);
                    _setIsBottomPanelVisible(true);
                }));

        // Phase 11 M5: test-results projection and show-on-test affordance.
        _testResultsViewModel.Activate();
        disposables.Add(_testResultsViewModel);
        disposables.Add(
            _projectWorkflowViewModel.WhenShowTestResultsRequested
                .Subscribe(_ =>
                {
                    _setBottomPanelMode(BottomPanelMode.TestResults);
                    _setIsBottomPanelVisible(true);
                }));

        // Keep the shared workspace + Source Control in sync with whichever
        // folder is loaded in the file tree, regardless of the open-folder entry
        // point (Ctrl+O via OpenFolderCommand or the file-tree "Open Folder..."
        // header, which invokes FileTreeViewModel.OpenFolderCommand directly).
        // RootPath is the single post-validation truth for the loaded folder, so
        // reacting to it prevents "No repository" when a repo is opened from the
        // file-tree header. M3: the null filter is removed so close transitions
        // also flow through SetProjectFromPath(null) and Source Control refresh.
        disposables.Add(
            _fileTreeViewModel.WhenAnyValue(x => x.RootPath)
                .Subscribe(path =>
                {
                    _workspace.SetProjectFromPath(path);
                    _setWorkspaceProjectName(_workspace.ProjectName);
                    _sourceControlViewModel.RefreshCommand.Execute(Unit.Default).Subscribe();
                }));

        // M3 (Phase 8.1.3): Bridge CloseFolderRequested interaction.
        // Executes CloseFolderCommand only when it can execute (folder is open).
        // Always completes the interaction output, including no-folder cases.
        // The handler is synchronous (not async void) so the interaction output
        // is set after the command body completes — callers that subscribe to
        // Handle() observe the post-command state immediately.
        disposables.Add(
            _fileTreeViewModel.CloseFolderRequested.RegisterHandler(interaction =>
            {
                if (_fileTreeViewModel.RootPath is not null)
                {
                    _closeFolderCommand.Execute().GetAwaiter().GetResult();
                }
                interaction.SetOutput(Unit.Default);
            }));

        // M4: Subscribe to authoritative project-context snapshots on the UI thread.
        // Resolve the scheduler at Activate time so tests can substitute
        // MainWindowViewModel.ProjectContextScheduler after construction.
        disposables.Add(
            _projectContextService.WhenChanged
                .ObserveOn(_getProjectContextScheduler())
                .Subscribe(ctx => _setCurrentProjectContext(ctx)));

        // Phase 9 M6: Clear status text on tab switch to prevent stale messages
        // from old tabs leaking into the current view. Each new event from the
        // active tab will set its own fresh status.
        disposables.Add(
            _editorTabs.WhenAnyValue(x => x.ActiveTab)
                .Subscribe(_ => _setStatusText(null)));

        // Phase 9 M6: Route fold status messages from the tab manager to the
        // status bar. Cleared on tab switch via the ActiveTab subscription above.
        disposables.Add(
            _editorTabs.WhenAnyValue(x => x.FoldStatusMessage)
                .Where(msg => msg is not null)
                .Subscribe(msg => _setStatusText(msg)));

        // Surface save errors from the tab manager
        disposables.Add(
            _editorTabs.WhenAnyValue(x => x.LastSaveError)
                .Where(msg => msg is not null)
                .Subscribe(msg => _setStatusText($"Save failed: {msg}")));

        disposables.Add(
            _editorTabs.WhenAnyValue(x => x.LastOpenError)
                .Where(msg => msg is not null)
                .Subscribe(msg => _setStatusText($"Open failed: {msg}")));

        disposables.Add(
            _terminalHost.StartupError
                .Where(err => err is not null)
                .Subscribe(err => _setStatusText($"Terminal: {err}")));

        // Subscribe to OpenFileRequested (published by RequestOpenFileCommand).
        // Uses the FileTreeNode payload directly — no dependency on SelectedFile.
        disposables.Add(
            _fileTreeViewModel.OpenFileRequested.Subscribe(node =>
            {
                var path = node.FullPath;
                var unsupported = SupportedFileTypes.GetUnsupportedMessage(path);

                if (unsupported is null)
                {
                    _editorTabs.OpenFileCommand.Execute(path).Subscribe(result =>
                    {
                        if (result)
                            _setStatusText($"Opened: {node.Name}");
                    });
                }
                else
                {
                    _setStatusText(unsupported);
                }
            }));
    }
}
