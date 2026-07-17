using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zaide.Features.Workspace.Domain;

/// <summary>
/// Represents a node in the file tree. Directories have Children; files are leaves.
/// Implements INotifyPropertyChanged directly instead of inheriting ReactiveObject.
/// </summary>
public class FileTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; init; }

    /// <summary>
    /// M3.4: Nesting level relative to the root. Root children have
    /// <c>Depth = 0</c>; each child directory is one level deeper.
    /// Set by <c>FileTreeService.EnumerateDirectory</c> and surfaced
    /// to the view for indent-guide rendering.
    /// </summary>
    public int Depth { get; set; }

    public ObservableCollection<FileTreeNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
