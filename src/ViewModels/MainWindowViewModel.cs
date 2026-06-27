using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace Zaide.ViewModels;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private bool _isBottomPanelVisible;
    private string? _statusText = "Open a folder to begin";
    private CompositeDisposable? _disposables;

    // Supported text-file extensions for opening in the editor.
    // Binary files and unknown extensions show a status message instead.
    private static readonly HashSet<string> SupportedExtensions = new(
        new[] { ".cs", ".json", ".md", ".txt", ".xml", ".axaml", ".csproj",
                ".sln", ".slnx", ".props", ".targets", ".config",
                ".editorconfig", ".gitignore", ".gitattributes", ".yml",
                ".yaml", ".css", ".html", ".js", ".ts", ".fs", ".vb",
                ".xaml", ".resx", ".razor", ".cshtml", ".svg" },
        StringComparer.OrdinalIgnoreCase);

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

    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveActiveTabCommand { get; }
    public Interaction<Unit, string?> PickFolder { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }
    public EditorTabViewModel EditorTabs { get; }

    public MainWindowViewModel(FileTreeViewModel fileTreeViewModel,
                               EditorTabViewModel editorTabViewModel)
    {
        FileTreeViewModel = fileTreeViewModel;
        EditorTabs = editorTabViewModel;
        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);
        SaveActiveTabCommand = ReactiveCommand.CreateFromTask(SaveActiveTabAsync);
        PickFolder = new Interaction<Unit, string?>();
        OpenFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await PickFolder.Handle(Unit.Default);
            if (path is not null)
                await FileTreeViewModel.OpenFolderCommand.Execute(path);
        });
    }

    /// <summary>
    /// Starts reactive subscriptions. Called by the View during activation.
    /// Safe to call multiple times — re-entrant guard prevents duplicates.
    /// </summary>
    public void Activate()
    {
        if (_disposables is not null) return;

        _disposables = new CompositeDisposable();

        // Surface save errors from the tab manager
        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.LastSaveError)
                .Where(msg => msg is not null)
                .Subscribe(msg => StatusText = $"Save failed: {msg}"));

        _disposables.Add(
            this.WhenAnyValue(x => x.EditorTabs.LastOpenError)
                .Where(msg => msg is not null)
                .Subscribe(msg => StatusText = $"Open failed: {msg}"));

        // Subscribe to RequestOpenFileCommand executions
        _disposables.Add(
            FileTreeViewModel.RequestOpenFileCommand.Subscribe(_ =>
            {
                var selected = FileTreeViewModel.SelectedFile;
                if (selected is null || selected.IsDirectory)
                    return;

                var path = selected.FullPath;
                var ext = Path.GetExtension(path);

                if (SupportedExtensions.Contains(ext))
                {
                    EditorTabs.OpenFileCommand.Execute(path).Subscribe(result =>
                    {
                        if (result)
                            StatusText = $"Opened: {selected.Name}";
                    });
                }
                else
                {
                    StatusText = ext.Length > 0
                        ? $"Unsupported file type: {ext}"
                        : "Unsupported file type: (no extension)";
                }
            }));
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