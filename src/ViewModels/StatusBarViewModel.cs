using System;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Services;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.ViewModels;

/// <summary>
/// UI-facing status-bar projection. The saved model label is deliberately
/// named configured because environment variables may override runtime use.
/// </summary>
public sealed class StatusBarViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    private string _caretText = "Ln 1, Col 1";
    private string _languageText = "—";
    private string _projectText = "Zaide";
    private string _branchText = "";
    private string? _configuredModel;
    private string _documentText = "—";
    private string _languageIntelligenceText = string.Empty;
    private string? _statusMessage;
    private bool _isSettingsOpen;

    public string CaretText { get => _caretText; private set => this.RaiseAndSetIfChanged(ref _caretText, value); }
    public string LanguageText { get => _languageText; private set => this.RaiseAndSetIfChanged(ref _languageText, value); }
    public string ProjectText { get => _projectText; private set => this.RaiseAndSetIfChanged(ref _projectText, value); }
    public string BranchText { get => _branchText; private set => this.RaiseAndSetIfChanged(ref _branchText, value); }
    public string? ConfiguredModel { get => _configuredModel; private set => this.RaiseAndSetIfChanged(ref _configuredModel, value); }

    /// <summary>
    /// Active document file name, or "—" when no tab is open.
    /// </summary>
    public string DocumentText { get => _documentText; private set => this.RaiseAndSetIfChanged(ref _documentText, value); }

    /// <summary>
    /// Phase 10 M7: C# language-session lifecycle projection (empty when not applicable).
    /// </summary>
    public string LanguageIntelligenceText
    {
        get => _languageIntelligenceText;
        private set => this.RaiseAndSetIfChanged(ref _languageIntelligenceText, value);
    }

    /// <summary>
    /// Transient status message (save/search/fold/command outcomes).
    /// null when no status is active.
    /// </summary>
    public string? StatusMessage { get => _statusMessage; private set => this.RaiseAndSetIfChanged(ref _statusMessage, value); }

    /// <summary>
    /// Mirrors <see cref="MainWindowViewModel.IsSettingsOpen"/> for status-bar button styling.
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenSettingsCommand { get; }

    public StatusBarViewModel(
        MainWindowViewModel mainWindow,
        ISettingsService settings,
        ILanguageSessionService languageSession)
        : this(mainWindow, settings, languageSession, AvaloniaScheduler.Instance)
    {
    }

    internal StatusBarViewModel(
        MainWindowViewModel mainWindow,
        ISettingsService settings,
        ILanguageSessionService languageSession,
        IScheduler scheduler)
    {
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await mainWindow.ShowSettings.Handle(System.Reactive.Unit.Default);
        });

        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.IsSettingsOpen)
            .ObserveOn(scheduler)
            .Subscribe(value => IsSettingsOpen = value));

        // Phase 9 M6: CaretText — include SelectionLength suffix when non-zero.
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.EditorTabs.ActiveTab)
            .Select(tab => tab is null
                ? Observable.Return("Ln 1, Col 1")
                : tab.WhenAnyValue(t => t.CaretLine, t => t.CaretColumn, t => t.SelectionLength,
                    (line, column, sel) => sel == 0
                        ? $"Ln {line}, Col {column}"
                        : $"Ln {line}, Col {column} | Sel {sel}")
                    .ObserveOn(scheduler))
            .Switch()
            .ObserveOn(scheduler)
            .Subscribe(value => CaretText = value));

        // Phase 9 M6: DocumentText — active file name or "—" when no tab.
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.EditorTabs.ActiveTab)
            .Select(tab => tab is null
                ? Observable.Return("—")
                : tab.WhenAnyValue(t => t.FileName)
                    .ObserveOn(scheduler))
            .Switch()
            .ObserveOn(scheduler)
            .Subscribe(value => DocumentText = value));

        // Phase 9 M6: StatusMessage — transient outcomes from save/search/fold/command.
        // Cleared on tab switch by MainWindowViewModel clearing StatusText.
        // Stale events from old tabs are suppressed by MainWindowViewModel's
        // stale-state check (capture-and-verify post-async pattern).
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.StatusText)
            .ObserveOn(scheduler)
            .Subscribe(value => StatusMessage = value));

        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.EditorTabs.ActiveTab)
            .Select(tab => tab is null ? "—" : GetLanguage(tab.FilePath))
            .ObserveOn(scheduler)
            .Subscribe(value => LanguageText = value));
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.CurrentProjectContext)
            .ObserveOn(scheduler)
            .Subscribe(ctx => ProjectText = MapProjectText(ctx)));
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.SourceControlViewModel.CurrentBranchName)
            .ObserveOn(scheduler)
            .Subscribe(value => BranchText = value ?? ""));
        _subscriptions.Add(settings.WhenChanged
            .StartWith(settings.Current)
            .ObserveOn(scheduler)
            .Subscribe(value => ConfiguredModel = string.IsNullOrWhiteSpace(value.Llm.Model) ? null : value.Llm.Model));

        LanguageIntelligenceText = LanguageSessionStatusPolicy.MapStatusBarText(languageSession.Current);
        _subscriptions.Add(languageSession.WhenChanged
            .StartWith(languageSession.Current)
            .ObserveOn(scheduler)
            .Subscribe(snapshot => LanguageIntelligenceText = LanguageSessionStatusPolicy.MapStatusBarText(snapshot)));
    }

    /// <summary>
    /// Maps a <see cref="ProjectContext"/> snapshot to the status-bar project text.
    /// </summary>
    private static string MapProjectText(ProjectContext? ctx)
    {
        if (ctx is null)
            return "Project error";
        switch (ctx.State)
        {
            case ProjectContextState.SingleProject:
            case ProjectContextState.Selected:
                return ctx.SelectedProject?.DisplayName ?? "Project error";
            case ProjectContextState.Loading:
                return "Loading…";
            case ProjectContextState.NoProject:
                return "No project";
            case ProjectContextState.Unsupported:
                return "Unsupported project";
            case ProjectContextState.Failed:
                return "Project error";
            case ProjectContextState.Unloaded:
                return "Zaide";
            default:
                return "Project error";
        }
    }

    private static string GetLanguage(string? path)
    {
        var extension = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "C#", ".ts" => "TypeScript", ".js" => "JavaScript", ".json" => "JSON",
            ".md" => "Markdown", ".xml" => "XML", ".html" => "HTML", ".css" => "CSS",
            ".py" => "Python", _ => extension.TrimStart('.').ToUpperInvariant()
        };
    }

    public void Dispose() => _subscriptions.Dispose();
}
