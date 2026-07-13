using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Projects structured diagnostics into the Problems surface and routes navigation
/// through the existing editor-tab / workspace path only.
/// </summary>
public sealed class ProblemsViewModel : ReactiveObject, IDisposable
{
    private readonly ILanguageDiagnosticsService _diagnosticsService;
    private readonly EditorTabViewModel _editorTabs;
    private readonly Workspace _workspace;
    private readonly CompositeDisposable _subscriptions = new();
    private LanguageSessionState _state = LanguageSessionState.Unavailable;
    private string? _statusMessage = "Language intelligence unavailable.";
    private LanguageSessionFailure? _failure;
    private long _sessionGeneration;
    private ProblemItemViewModel? _selectedProblem;
    private bool _disposed;

    /// <summary>
    /// Scheduler for diagnostics subscription. Internal so tests can substitute
    /// a deterministic scheduler without a constructor parameter.
    /// </summary>
    internal System.Reactive.Concurrency.IScheduler Scheduler { get; set; }
        = AvaloniaScheduler.Instance;

    public ObservableCollection<ProblemItemViewModel> Problems { get; } = new();

    public LanguageSessionState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public LanguageSessionFailure? Failure
    {
        get => _failure;
        private set => this.RaiseAndSetIfChanged(ref _failure, value);
    }

    public long SessionGeneration
    {
        get => _sessionGeneration;
        private set => this.RaiseAndSetIfChanged(ref _sessionGeneration, value);
    }

    public int ProblemCount => Problems.Count;

    public ProblemItemViewModel? SelectedProblem
    {
        get => _selectedProblem;
        set => this.RaiseAndSetIfChanged(ref _selectedProblem, value);
    }

    public ReactiveCommand<ProblemItemViewModel?, Unit> NavigateToProblemCommand { get; }

    public ProblemsViewModel(
        ILanguageDiagnosticsService diagnosticsService,
        EditorTabViewModel editorTabs,
        Workspace workspace)
    {
        _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        NavigateToProblemCommand = ReactiveCommand.CreateFromTask<ProblemItemViewModel?>(
            NavigateToProblemAsync);

        // Defer subscription start until Activate() so tests can set Scheduler first.
    }

    /// <summary>
    /// Starts projecting diagnostics. Safe to call once; subsequent calls are no-ops.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        ApplySnapshot(_diagnosticsService.Current);

        _subscriptions.Add(
            _diagnosticsService.WhenChanged
                .ObserveOn(Scheduler)
                .Subscribe(ApplySnapshot));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscriptions.Dispose();
    }

    private void ApplySnapshot(LanguageDiagnosticsSnapshot snapshot)
    {
        State = snapshot.State;
        Failure = snapshot.Failure;
        SessionGeneration = snapshot.SessionGeneration;
        StatusMessage = BuildStatusMessage(snapshot);

        Problems.Clear();
        foreach (var diagnostic in snapshot.Diagnostics)
            Problems.Add(new ProblemItemViewModel(diagnostic));

        this.RaisePropertyChanged(nameof(ProblemCount));

        if (SelectedProblem is not null &&
            Problems.All(p => !ReferenceEquals(p.Diagnostic, SelectedProblem.Diagnostic) &&
                              !SameIdentity(p, SelectedProblem)))
        {
            SelectedProblem = null;
        }
    }

    private static bool SameIdentity(ProblemItemViewModel a, ProblemItemViewModel b) =>
        string.Equals(a.DocumentUri, b.DocumentUri, StringComparison.Ordinal) &&
        a.DocumentVersion == b.DocumentVersion &&
        a.SessionGeneration == b.SessionGeneration &&
        a.StartOffset == b.StartOffset &&
        a.EndOffset == b.EndOffset &&
        string.Equals(a.Message, b.Message, StringComparison.Ordinal);

    private static string? BuildStatusMessage(LanguageDiagnosticsSnapshot snapshot) =>
        LanguageSessionStatusPolicy.MapProblemsStatusMessage(snapshot);

    /// <summary>
    /// Opens/activates the diagnostic's file through <see cref="EditorTabViewModel"/>
    /// and requests a caret/selection jump only when the location is still live.
    /// Stale/closed/invalid targets no-op safely.
    /// </summary>
    internal async Task<bool> NavigateToProblemAsync(ProblemItemViewModel? item)
    {
        if (item is null || _disposed)
            return false;

        // Re-validate against the live diagnostics snapshot immediately before navigation.
        var snapshot = _diagnosticsService.Current;
        if (snapshot.State != LanguageSessionState.Ready ||
            snapshot.SessionGeneration != item.SessionGeneration)
        {
            return false;
        }

        var live = snapshot.Diagnostics.FirstOrDefault(d =>
            string.Equals(d.DocumentUri, item.DocumentUri, StringComparison.Ordinal) &&
            d.DocumentVersion == item.DocumentVersion &&
            d.SessionGeneration == item.SessionGeneration &&
            d.StartOffset == item.StartOffset &&
            d.EndOffset == item.EndOffset &&
            string.Equals(d.Message, item.Message, StringComparison.Ordinal));

        if (live is null)
            return false;

        if (string.IsNullOrWhiteSpace(live.FilePath))
            return false;

        // Open/activate only through the existing editor-tab path.
        var opened = await _editorTabs.OpenFileCommand.Execute(live.FilePath);
        if (!opened)
            return false;

        var tab = _editorTabs.ActiveTab;
        if (tab is null ||
            !string.Equals(tab.FilePath, live.FilePath, StringComparison.Ordinal))
        {
            return false;
        }

        // Re-validate offsets against current document text (may have changed).
        var content = tab.Document.Content;
        if (!LspUtf16PositionMapper.TryMapRange(
                content,
                live.Range,
                out var startOffset,
                out var endOffset))
        {
            return false;
        }

        if (startOffset < 0 || endOffset < startOffset || endOffset > content.Length)
            return false;

        tab.RequestNavigate(startOffset, endOffset - startOffset);
        return true;
    }
}
