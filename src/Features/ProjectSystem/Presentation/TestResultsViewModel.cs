using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Contracts;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// Projects <see cref="ITestResultsService"/> into the Test Results surface.
/// </summary>
public sealed class TestResultsViewModel : ReactiveObject, IDisposable
{
    private readonly ITestResultsService _testResultsService;
    private readonly EditorTabViewModel _editorTabs;
    private readonly ProjectWorkflowViewModel _workflow;
    private readonly CompositeDisposable _subscriptions = new();
    private string? _summaryText;
    private string? _statusMessage;
    private TestCaseItemViewModel? _selectedCase;
    private bool _disposed;

    /// <summary>
    /// Scheduler for test-results subscription. Internal so tests can substitute
    /// a deterministic scheduler without a constructor parameter.
    /// </summary>
    internal System.Reactive.Concurrency.IScheduler Scheduler { get; set; }
        = AvaloniaScheduler.Instance;

    public ObservableCollection<TestCaseItemViewModel> Cases { get; } = new();

    public string? SummaryText
    {
        get => _summaryText;
        private set => this.RaiseAndSetIfChanged(ref _summaryText, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public TestCaseItemViewModel? SelectedCase
    {
        get => _selectedCase;
        set => this.RaiseAndSetIfChanged(ref _selectedCase, value);
    }

    public ReactiveCommand<TestCaseItemViewModel?, Unit> NavigateToCaseCommand { get; }

    /// <summary>
    /// Shared workflow projection for Cancel on Test Results (same command as Output).
    /// </summary>
    public ProjectWorkflowViewModel Workflow => _workflow;

    public TestResultsViewModel(
        ITestResultsService testResultsService,
        EditorTabViewModel editorTabs,
        ProjectWorkflowViewModel workflow)
    {
        _testResultsService = testResultsService ??
                              throw new ArgumentNullException(nameof(testResultsService));
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));

        NavigateToCaseCommand = ReactiveCommand.CreateFromTask<TestCaseItemViewModel?>(
            NavigateToCaseAsync,
            this.WhenAnyValue(x => x.SelectedCase)
                .Select(item => item is not null && item.CanNavigate));
    }

    /// <summary>
    /// Starts projecting test results. Safe to call once; subsequent calls are no-ops.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        ApplySnapshot(_testResultsService.Current);

        _subscriptions.Add(
            _testResultsService.WhenChanged
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

    private void ApplySnapshot(TestResultsSnapshot snapshot)
    {
        Cases.Clear();
        foreach (var testCase in snapshot.Cases)
            Cases.Add(new TestCaseItemViewModel(testCase));

        SummaryText = FormatSummary(snapshot.Summary);
        StatusMessage = FormatStatus(snapshot);
    }

    private static string? FormatSummary(TestResultsSummary? summary)
    {
        if (summary is null)
            return null;

        if (summary.Total is null && summary.Passed is null && summary.Failed is null &&
            summary.Skipped is null)
            return null;

        return
            $"Passed: {summary.Passed?.ToString() ?? "?"}  Failed: {summary.Failed?.ToString() ?? "?"}  " +
            $"Skipped: {summary.Skipped?.ToString() ?? "?"}  Total: {summary.Total?.ToString() ?? "?"}";
    }

    private static string? FormatStatus(TestResultsSnapshot snapshot)
    {
        if (snapshot.Generation == 0 && snapshot.OperationOutcome is null)
            return "No test results yet.";

        if (snapshot.OperationOutcome is null)
            return "Running tests…";

        if (snapshot.IsPartial && snapshot.Cases.Count == 0 && snapshot.Summary is null)
        {
            return snapshot.OperationOutcome switch
            {
                ProjectWorkflowOutcomeKind.Cancelled => "Tests cancelled. See Output for raw log.",
                ProjectWorkflowOutcomeKind.Succeeded => "Tests finished. See Output for raw log.",
                ProjectWorkflowOutcomeKind.Failed => "Tests failed. See Output for raw log.",
                _ => "Test run finished. See Output for raw log.",
            };
        }

        if (snapshot.IsPartial)
            return "Partial test results (run cancelled or console parse incomplete).";

        return snapshot.OperationOutcome switch
        {
            ProjectWorkflowOutcomeKind.Succeeded => "All tests passed.",
            ProjectWorkflowOutcomeKind.Failed => "One or more tests failed.",
            ProjectWorkflowOutcomeKind.Cancelled => "Tests cancelled.",
            ProjectWorkflowOutcomeKind.StartupFailed => "Tests could not start.",
            _ => null,
        };
    }

    private async Task NavigateToCaseAsync(TestCaseItemViewModel? item)
    {
        item ??= SelectedCase;
        if (item is null || !item.CanNavigate)
            return;

        var result = item.Result;
        if (string.IsNullOrWhiteSpace(result.FilePath) || result.Line is not int line || line <= 0)
            return;

        var opened = await _editorTabs.OpenFileCommand.Execute(result.FilePath);
        if (!opened)
            return;

        var tab = _editorTabs.ActiveTab;
        if (tab is null || !string.Equals(tab.FilePath, result.FilePath, StringComparison.Ordinal))
            return;

        var content = tab.Document.Content;
        if (!LspUtf16PositionMapper.TryGetOffset(content, line - 1, 0, out var startOffset))
            return;

        if (startOffset < 0 || startOffset > content.Length)
            return;

        tab.RequestNavigate(startOffset, 0);
    }
}
