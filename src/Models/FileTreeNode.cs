using System.Collections.ObjectModel;
using ReactiveUI;

namespace Zaide.Models;

/// <summary>
/// Represents a node in the file tree. Directories have Children; files are leaves.
/// Inherits ReactiveObject so IsExpanded can be bound to TreeView expansion state.
/// </summary>
public class FileTreeNode : ReactiveObject
{
    private bool _isExpanded;

    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public ObservableCollection<FileTreeNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}
