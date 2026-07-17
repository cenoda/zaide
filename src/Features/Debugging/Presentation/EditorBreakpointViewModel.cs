using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// Owns editor breakpoint commands, margin projection, and active-session DAP
/// replacement after persistence mutations.
/// </summary>
public sealed class EditorBreakpointViewModel : ReactiveObject, IDisposable
{
    private readonly EditorTabViewModel _editorTabs;
    private readonly IBreakpointService _breakpointService;
    private readonly IDebugSessionService _debugSession;
    private readonly IProjectContextService _projectContext;
    private readonly ISettingsService _settings;
    private readonly CompositeDisposable _subscriptions = new();
    private IReadOnlyList<EditorBreakpointMarker> _markers = Array.Empty<EditorBreakpointMarker>();
    private string? _activeDocumentPath;
    private long _projectionRevision;
    private bool _disposed;

    public IReadOnlyList<EditorBreakpointMarker> Markers
    {
        get => _markers;
        private set => this.RaiseAndSetIfChanged(ref _markers, value);
    }

    /// <summary>
    /// Normalized absolute path for the active on-disk document, or null.
    /// </summary>
    public string? ActiveDocumentPath
    {
        get => _activeDocumentPath;
        private set => this.RaiseAndSetIfChanged(ref _activeDocumentPath, value);
    }

    /// <summary>
    /// Monotonic token bumped whenever margin projection changes.
    /// </summary>
    public long ProjectionRevision
    {
        get => _projectionRevision;
        private set => this.RaiseAndSetIfChanged(ref _projectionRevision, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleBreakpointCommand { get; }

    public ReactiveCommand<int, Unit> ToggleAtLineCommand { get; }

    public EditorBreakpointViewModel(
        EditorTabViewModel editorTabs,
        IBreakpointService breakpointService,
        IDebugSessionService debugSession,
        IProjectContextService projectContext,
        ISettingsService settings,
        ICommandRegistry? commandRegistry = null)
    {
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
        _breakpointService = breakpointService ?? throw new ArgumentNullException(nameof(breakpointService));
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        var hasToggleContext = CreateHasToggleContext();

        ToggleAtLineCommand = ReactiveCommand.CreateFromTask<int, Unit>(
            async line =>
            {
                await ToggleAtLineAsync(line).ConfigureAwait(false);
                return Unit.Default;
            });

        var canToggleBreakpoint = hasToggleContext.CombineLatest(
            _editorTabs.WhenAnyValue(x => x.ActiveTab)
                .Select(tab => tab is null
                    ? Observable.Return(false)
                    : tab.WhenAnyValue(x => x.CaretLine, x => x.TextContent)
                        .Select(values => EditorBreakpointProjection.IsValidCaretLine(
                            values.Item1,
                            values.Item2)))
                .Switch(),
            (hasContext, validCaret) => hasContext && validCaret);

        ToggleBreakpointCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                var tab = _editorTabs.ActiveTab;
                if (tab is null)
                    return;

                await ToggleAtLineAsync(tab.CaretLine).ConfigureAwait(false);
            },
            canToggleBreakpoint);

        commandRegistry?.Register(new CommandDescriptor(
            "debug.toggleBreakpoint",
            "Toggle Breakpoint",
            "Debug",
            new[] { "F9" },
            ToggleBreakpointCommand));
    }

    /// <summary>
    /// Starts reactive projection subscriptions. Safe to call once.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        _subscriptions.Add(
            _editorTabs.WhenAnyValue(x => x.ActiveTab)
                .Select(tab => tab is null
                    ? Observable.Return((EditorViewModel?)null)
                    : tab.WhenAnyValue(x => x.FilePath).Select(_ => tab))
                .Switch()
                .Subscribe(_ => RefreshProjection()));

        _subscriptions.Add(
            _settings.WhenChanged.Subscribe(_ => RefreshProjection()));

        _subscriptions.Add(
            _projectContext.WhenChanged.Subscribe(_ => RefreshProjection()));

        _subscriptions.Add(
            _debugSession.WhenChanged.Subscribe(_ => RefreshProjection()));

        RefreshProjection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscriptions.Dispose();
    }

    private IObservable<bool> CreateHasToggleContext()
    {
        var workspaceRoot = _projectContext.WhenChanged
            .StartWith(_projectContext.Current)
            .Select(context => context.WorkspaceRoot);

        return _editorTabs.WhenAnyValue(x => x.ActiveTab)
            .Select(tab =>
            {
                if (tab is null)
                    return Observable.Return(false);

                return tab.WhenAnyValue(x => x.FilePath)
                    .CombineLatest(
                        workspaceRoot,
                        (filePath, root) =>
                            EditorBreakpointProjection.NormalizeDocumentPath(filePath) is not null &&
                            EditorBreakpointProjection.HasSelectedWorkspace(root));
            })
            .Switch()
            .DistinctUntilChanged();
    }

    private bool IsValidToggleLine(int line)
    {
        var tab = _editorTabs.ActiveTab;
        return tab is not null &&
               EditorBreakpointProjection.IsValidCaretLine(line, tab.TextContent);
    }

    private async Task ToggleAtLineAsync(int line)
    {
        var tab = _editorTabs.ActiveTab;
        if (tab is null)
            return;

        var sourcePath = EditorBreakpointProjection.NormalizeDocumentPath(tab.FilePath);
        if (sourcePath is null)
            return;

        if (!EditorBreakpointProjection.HasSelectedWorkspace(_projectContext.Current.WorkspaceRoot))
            return;

        if (!EditorBreakpointProjection.IsValidCaretLine(line, tab.TextContent))
            return;

        var result = await _breakpointService.ToggleAsync(sourcePath, line).ConfigureAwait(false);
        if (!result.Succeeded)
            return;

        RefreshProjection();
        await SyncDapReplacementAsync(sourcePath).ConfigureAwait(false);
    }

    private void RefreshProjection()
    {
        var tab = _editorTabs.ActiveTab;
        var sourcePath = tab is null
            ? null
            : EditorBreakpointProjection.NormalizeDocumentPath(tab.FilePath);

        ActiveDocumentPath = sourcePath;

        if (sourcePath is null ||
            !EditorBreakpointProjection.HasSelectedWorkspace(_projectContext.Current.WorkspaceRoot))
        {
            Markers = Array.Empty<EditorBreakpointMarker>();
        }
        else
        {
            Markers = EditorBreakpointProjection.ForSource(
                _breakpointService.GetBreakpoints(),
                sourcePath,
                _debugSession.Current.BreakpointVerifications);
        }

        ProjectionRevision++;
    }

    private async Task SyncDapReplacementAsync(string sourcePath)
    {
        var state = _debugSession.Current.State;
        if (state is not (DebugSessionState.Running or DebugSessionState.Stopped))
            return;

        var replacement = _breakpointService.MapToDapReplacementBySource(new[] { sourcePath });
        await _debugSession.ReplaceBreakpointsBySourceAsync(replacement).ConfigureAwait(false);
    }
}