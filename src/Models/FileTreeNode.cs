using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zaide.Models;

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
