using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Presentation row for a direct conversation in the Townhall sidebar.
/// </summary>
internal sealed class TownhallNavigationItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _hasUnread;

    public required ConversationId ConversationId { get; init; }

    public required TownhallNavigationKind Kind { get; init; }

    public required string Label { get; init; }

    public ActorId? PeerActorId { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool HasUnread
    {
        get => _hasUnread;
        set
        {
            if (_hasUnread == value)
            {
                return;
            }

            _hasUnread = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
