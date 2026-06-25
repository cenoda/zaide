using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Reactive bridge between FileTreeService and the file tree view.
/// Holds root nodes, open-folder command, selected file, and current path.
/// </summary>
public class FileTreeViewModel : ReactiveObject
{
    private readonly FileTreeService _fileTreeService;
    private FileTreeNode? _selectedFile;
    private string? _rootPath;

    public ObservableCollection<FileTreeNode> RootNodes { get; } = new();

    public ReactiveCommand<string, Unit> OpenFolderCommand { get; }

    public FileTreeNode? SelectedFile
    {
        get => _selectedFile;
        set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
    }

    public string? RootPath
    {
        get => _rootPath;
        set => this.RaiseAndSetIfChanged(ref _rootPath, value);
    }

    public FileTreeViewModel(FileTreeService fileTreeService)
    {
        _fileTreeService = fileTreeService;

        OpenFolderCommand = ReactiveCommand.Create<string>(path =>
        {
            RootPath = path;
            var nodes = _fileTreeService.EnumerateDirectory(path);
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);
        });
    }
}
