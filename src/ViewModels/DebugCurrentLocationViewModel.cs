using System;
using System.IO;
using System.Threading;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.Editor.Presentation;

namespace Zaide.ViewModels;

/// <summary>
/// Projects the selected stopped stack frame into editor navigation and a
/// distinct instruction-pointer marker. Does not mutate breakpoint data.
/// </summary>
public sealed class DebugCurrentLocationViewModel : ReactiveObject, IDisposable
{
    private readonly IDebugSessionService _debugSession;
    private readonly DebugStackProjectionViewModel _stackProjection;
    private readonly EditorTabViewModel _editorTabs;
    private readonly CompositeDisposable _subscriptions = new();
    private EditorInstructionPointerMarker? _marker;
    private string? _activeDocumentPath;
    private string? _statusMessage;
    private long _projectionRevision;
    private long _navigationToken;
    private bool _disposed;

    public DebugCurrentLocationViewModel(
        IDebugSessionService debugSession,
        DebugStackProjectionViewModel stackProjection,
        EditorTabViewModel editorTabs)
    {
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));
        _stackProjection = stackProjection ?? throw new ArgumentNullException(nameof(stackProjection));
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
    }

    public EditorInstructionPointerMarker? Marker
    {
        get => _marker;
        private set => this.RaiseAndSetIfChanged(ref _marker, value);
    }

    public string? ActiveDocumentPath
    {
        get => _activeDocumentPath;
        private set => this.RaiseAndSetIfChanged(ref _activeDocumentPath, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public long ProjectionRevision
    {
        get => _projectionRevision;
        private set => this.RaiseAndSetIfChanged(ref _projectionRevision, value);
    }

    /// <summary>
    /// Starts reactive current-location projection. Safe to call once.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        _subscriptions.Add(
            _debugSession.WhenChanged.Subscribe(snapshot =>
            {
                if (snapshot.State != DebugSessionState.Stopped)
                    ClearProjection();
            }));

        _subscriptions.Add(
            _stackProjection.WhenAnyValue(x => x.SelectedFrame)
                .Subscribe(frame => ObserveTask(ProjectSelectedFrameAsync(frame))));

        ClearProjection();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscriptions.Dispose();
    }

    private async Task ProjectSelectedFrameAsync(DebugStackFrameViewModel? frame)
    {
        var navigationToken = Interlocked.Increment(ref _navigationToken);

        if (_debugSession.Current.State != DebugSessionState.Stopped || frame is null)
        {
            ClearProjection();
            return;
        }

        if (string.IsNullOrWhiteSpace(frame.SourcePath) || frame.Line is not int line || line < 1)
        {
            ClearMarker();
            StatusMessage = "Current execution location is unavailable for the selected frame.";
            BumpRevision();
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(frame.SourcePath);
        }
        catch
        {
            ClearMarker();
            StatusMessage = "Current execution location uses an invalid source path.";
            BumpRevision();
            return;
        }

        if (!File.Exists(normalizedPath))
        {
            ClearMarker();
            StatusMessage = $"Source file not found: {normalizedPath}";
            BumpRevision();
            return;
        }

        var opened = await _editorTabs.OpenFileCommand.Execute(normalizedPath).FirstAsync();
        if (navigationToken != _navigationToken ||
            _debugSession.Current.State != DebugSessionState.Stopped ||
            !ReferenceEquals(frame, _stackProjection.SelectedFrame))
        {
            return;
        }

        if (!opened)
        {
            ClearMarker();
            StatusMessage = $"Could not open source file: {normalizedPath}";
            BumpRevision();
            return;
        }

        var tab = _editorTabs.ActiveTab;
        if (tab is null ||
            !string.Equals(
                EditorBreakpointProjection.NormalizeDocumentPath(tab.FilePath),
                normalizedPath,
                StringComparison.Ordinal))
        {
            ClearMarker();
            StatusMessage = $"Could not activate source file: {normalizedPath}";
            BumpRevision();
            return;
        }

        var content = tab.Document.Content;
        if (!LspUtf16PositionMapper.TryGetOffset(content, line - 1, 0, out var startOffset) ||
            startOffset < 0 ||
            startOffset > content.Length)
        {
            ClearMarker();
            ActiveDocumentPath = normalizedPath;
            StatusMessage = $"Source line {line} is unavailable in {Path.GetFileName(normalizedPath)}.";
            BumpRevision();
            return;
        }

        tab.RequestNavigate(startOffset, 0);
        ActiveDocumentPath = normalizedPath;
        Marker = new EditorInstructionPointerMarker(line);
        StatusMessage = null;
        BumpRevision();
    }

    private void ClearProjection()
    {
        Interlocked.Increment(ref _navigationToken);
        Marker = null;
        ActiveDocumentPath = null;
        StatusMessage = null;
        BumpRevision();
    }

    private void ClearMarker()
    {
        Marker = null;
        ActiveDocumentPath = null;
    }

    private void BumpRevision() => ProjectionRevision++;

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}