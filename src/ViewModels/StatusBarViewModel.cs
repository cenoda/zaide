using System;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Services;

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

    public string CaretText { get => _caretText; private set => this.RaiseAndSetIfChanged(ref _caretText, value); }
    public string LanguageText { get => _languageText; private set => this.RaiseAndSetIfChanged(ref _languageText, value); }
    public string ProjectText { get => _projectText; private set => this.RaiseAndSetIfChanged(ref _projectText, value); }
    public string BranchText { get => _branchText; private set => this.RaiseAndSetIfChanged(ref _branchText, value); }
    public string? ConfiguredModel { get => _configuredModel; private set => this.RaiseAndSetIfChanged(ref _configuredModel, value); }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenSettingsCommand { get; }

    public StatusBarViewModel(MainWindowViewModel mainWindow, ISettingsService settings)
        : this(mainWindow, settings, AvaloniaScheduler.Instance)
    {
    }

    internal StatusBarViewModel(MainWindowViewModel mainWindow, ISettingsService settings, IScheduler scheduler)
    {
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await mainWindow.ShowSettings.Handle(System.Reactive.Unit.Default);
        });

        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.EditorTabs.ActiveTab)
            .Select(tab => tab is null
                ? Observable.Return("Ln 1, Col 1")
                : tab.WhenAnyValue(t => t.CaretLine, t => t.CaretColumn,
                    (line, column) => $"Ln {line}, Col {column}")
                    .ObserveOn(scheduler))
            .Switch()
            .ObserveOn(scheduler)
            .Subscribe(value => CaretText = value));
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.EditorTabs.ActiveTab)
            .Select(tab => tab is null ? "—" : GetLanguage(tab.FilePath))
            .ObserveOn(scheduler)
            .Subscribe(value => LanguageText = value));
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.WorkspaceProjectName)
            .ObserveOn(scheduler)
            .Subscribe(value => ProjectText = value ?? "Zaide"));
        _subscriptions.Add(mainWindow.WhenAnyValue(x => x.SourceControlViewModel.CurrentBranchName)
            .ObserveOn(scheduler)
            .Subscribe(value => BranchText = value ?? ""));
        _subscriptions.Add(settings.WhenChanged
            .StartWith(settings.Current)
            .ObserveOn(scheduler)
            .Subscribe(value => ConfiguredModel = string.IsNullOrWhiteSpace(value.Llm.Model) ? null : value.Llm.Model));
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
