using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace Zaide.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private bool _isBottomPanelVisible;
    private string? _statusText = "Open a folder to begin";

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

    public FileTreeViewModel FileTreeViewModel { get; }
    public EditorTabViewModel EditorTabs { get; }

    public MainWindowViewModel(FileTreeViewModel fileTreeViewModel,
                               EditorTabViewModel editorTabViewModel)
    {
        FileTreeViewModel = fileTreeViewModel;
        EditorTabs = editorTabViewModel;

        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);

        // When user selects a file in the tree, open it in the editor
        this.WhenAnyValue(x => x.FileTreeViewModel.SelectedFile)
            .Where(file => file is not null && !file.IsDirectory)
            .Subscribe(file =>
            {
                var path = file!.FullPath;
                var ext = Path.GetExtension(path);

                if (SupportedExtensions.Contains(ext))
                {
                    EditorTabs.OpenFileCommand.Execute(path).Subscribe();
                    StatusText = $"Opened: {file.Name}";
                }
                else
                {
                    StatusText = ext.Length > 0
                        ? $"Unsupported file type: {ext}"
                        : $"Opened: {file.Name}";
                }
            });
    }

    private void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }
}
