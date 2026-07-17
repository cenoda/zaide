using System;
using System.Collections.Generic;
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
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// Projects structured language and build diagnostics into the Problems surface
/// and routes navigation through the existing editor-tab / workspace path only.
/// </summary>
public sealed class ProblemsViewModel : ReactiveObject, IDisposable
{
    private readonly ILanguageDiagnosticsService _diagnosticsService;
    private readonly IBuildDiagnosticsService _buildDiagnosticsService;
    private readonly EditorTabViewModel _editorTabs;
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly List<ProblemItemViewModel> _languageProblems = new();
    private readonly List<ProblemItemViewModel> _buildProblems = new();
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
        IBuildDiagnosticsService buildDiagnosticsService,
        EditorTabViewModel editorTabs,
        global::Zaide.Features.Workspace.Domain.Workspace workspace)
    {
        _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
        _buildDiagnosticsService = buildDiagnosticsService ??
                                   throw new ArgumentNullException(nameof(buildDiagnosticsService));
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

        ApplyLanguageSnapshot(_diagnosticsService.Current);
        ApplyBuildSnapshot(_buildDiagnosticsService.Current);

        _subscriptions.Add(
            _diagnosticsService.WhenChanged
                .ObserveOn(Scheduler)
                .Subscribe(ApplyLanguageSnapshot));

        _subscriptions.Add(
            _buildDiagnosticsService.WhenChanged
                .ObserveOn(Scheduler)
                .Subscribe(ApplyBuildSnapshot));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscriptions.Dispose();
    }

    private void ApplyLanguageSnapshot(LanguageDiagnosticsSnapshot snapshot)
    {
        State = snapshot.State;
        Failure = snapshot.Failure;
        SessionGeneration = snapshot.SessionGeneration;
        StatusMessage = BuildStatusMessage(snapshot);

        _languageProblems.Clear();
        foreach (var diagnostic in snapshot.Diagnostics)
            _languageProblems.Add(new ProblemItemViewModel(diagnostic));

        RebuildProblemsList();
    }

    private void ApplyBuildSnapshot(BuildDiagnosticsSnapshot snapshot)
    {
        _buildProblems.Clear();
        foreach (var diagnostic in snapshot.Diagnostics)
            _buildProblems.Add(new ProblemItemViewModel(diagnostic, snapshot.BuildGeneration));

        RebuildProblemsList();
    }

    private void RebuildProblemsList()
    {
        Problems.Clear();
        foreach (var item in _languageProblems)
            Problems.Add(item);
        foreach (var item in _buildProblems)
            Problems.Add(item);

        this.RaisePropertyChanged(nameof(ProblemCount));

        if (SelectedProblem is not null &&
            Problems.All(p => !SameIdentity(p, SelectedProblem)))
        {
            SelectedProblem = null;
        }
    }

    private static bool SameIdentity(ProblemItemViewModel a, ProblemItemViewModel b)
    {
        if (a.Kind != b.Kind)
            return false;

        return a.Kind switch
        {
            ProblemKind.Language =>
                ReferenceEquals(a.Diagnostic, b.Diagnostic) ||
                (string.Equals(a.DocumentUri, b.DocumentUri, StringComparison.Ordinal) &&
                 a.DocumentVersion == b.DocumentVersion &&
                 a.SessionGeneration == b.SessionGeneration &&
                 a.StartOffset == b.StartOffset &&
                 a.EndOffset == b.EndOffset &&
                 string.Equals(a.Message, b.Message, StringComparison.Ordinal)),
            ProblemKind.Build =>
                a.BuildGeneration == b.BuildGeneration &&
                string.Equals(a.FilePath, b.FilePath, StringComparison.Ordinal) &&
                a.Line == b.Line &&
                a.Column == b.Column &&
                a.Severity == b.Severity &&
                string.Equals(a.Code, b.Code, StringComparison.Ordinal) &&
                string.Equals(a.Message, b.Message, StringComparison.Ordinal),
            _ => false,
        };
    }

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

        return item.Kind switch
        {
            ProblemKind.Language => await NavigateToLanguageProblemAsync(item).ConfigureAwait(false),
            ProblemKind.Build => await NavigateToBuildProblemAsync(item).ConfigureAwait(false),
            _ => false,
        };
    }

    private async Task<bool> NavigateToLanguageProblemAsync(ProblemItemViewModel item)
    {
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

        var opened = await _editorTabs.OpenFileCommand.Execute(live.FilePath);
        if (!opened)
            return false;

        var tab = _editorTabs.ActiveTab;
        if (tab is null ||
            !string.Equals(tab.FilePath, live.FilePath, StringComparison.Ordinal))
        {
            return false;
        }

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

    private async Task<bool> NavigateToBuildProblemAsync(ProblemItemViewModel item)
    {
        var snapshot = _buildDiagnosticsService.Current;
        if (snapshot.BuildGeneration != item.BuildGeneration)
            return false;

        var live = snapshot.Diagnostics.FirstOrDefault(d =>
            string.Equals(d.FilePath, item.FilePath, StringComparison.Ordinal) &&
            d.Line == item.Line &&
            d.Column == item.Column &&
            d.Severity == item.Severity &&
            string.Equals(d.Code, item.Code, StringComparison.Ordinal) &&
            string.Equals(d.Message, item.Message, StringComparison.Ordinal));

        if (live is null || string.IsNullOrWhiteSpace(live.FilePath))
            return false;

        var opened = await _editorTabs.OpenFileCommand.Execute(live.FilePath);
        if (!opened)
            return false;

        var tab = _editorTabs.ActiveTab;
        if (tab is null ||
            !string.Equals(tab.FilePath, live.FilePath, StringComparison.Ordinal))
        {
            return false;
        }

        var content = tab.Document.Content;
        var line = live.Line - 1;
        var column = live.Column > 0 ? live.Column - 1 : 0;
        if (!LspUtf16PositionMapper.TryGetOffset(content, line, column, out var startOffset))
            return false;

        if (startOffset < 0 || startOffset > content.Length)
            return false;

        tab.RequestNavigate(startOffset, 0);
        return true;
    }
}
