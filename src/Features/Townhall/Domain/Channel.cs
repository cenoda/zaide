using System;
using System.Runtime.CompilerServices;

namespace Zaide.Features.Townhall.Domain;

/// <summary>
/// Represents a channel in the Townhall workspace.
/// Implements INotifyPropertyChanged for UI binding to Active state.
/// </summary>
public class Channel : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private bool _isPinned;
    private bool _isActive;
    private bool _hasUnread;

    /// <summary>
    /// Unique identifier for the channel.
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Display name of the channel (without # prefix).
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this channel is pinned to the top of the list.
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this channel is currently active.
    /// Raises PropertyChanged for UI binding.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this channel has unread activity relative to the last-read cursor.
    /// Presentation-only; raised via PropertyChanged for nav affordance.
    /// </summary>
    public bool HasUnread
    {
        get => _hasUnread;
        set
        {
            if (_hasUnread != value)
            {
                _hasUnread = value;
                OnPropertyChanged();
            }
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
