using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Zaide.Models;

namespace Zaide.Views;

/// <summary>
/// Channels panel for the Townhall sidebar.
/// Shows channel names prefixed with "#", active channel highlight, pin icons.
/// Clicking a channel invokes SelectChannelCommand on the ViewModel.
/// Matches M0.5 palette and M3 spec.
/// </summary>
public class TownhallChannelPanel : Panel
{
    private readonly StackPanel _channelList;
    private Action<string>? _onChannelSelected;

    public TownhallChannelPanel()
    {
        var header = new TextBlock
        {
            Text = "Channels",
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(12, 12, 12, 4)
        };

        _channelList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        var scrollViewer = new ScrollViewer
        {
            Content = _channelList,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Children.Add(new DockPanel
        {
            Children =
            {
                header,
                scrollViewer
            }
        });

        DockPanel.SetDock(header, Dock.Top);
    }

    /// <summary>
    /// Sets the callback invoked when a channel is clicked.
    /// </summary>
    public void SetOnChannelSelected(Action<string> onSelected)
    {
        _onChannelSelected = onSelected;
    }

    /// <summary>
    /// Populates the channel list. Called when channels change.
    /// </summary>
    public void SetChannels(ObservableCollection<Channel> channels)
    {
        _channelList.Children.Clear();

        foreach (var channel in channels)
        {
            _channelList.Children.Add(CreateChannelRow(channel));
        }
    }

    private Border CreateChannelRow(Channel channel)
    {
        // Channel name with # prefix
        var nameText = new TextBlock
        {
            Text = $"#{channel.Name}",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = channel.IsActive
                ? (IBrush?)Application.Current!.Resources["TextPrimaryBrush"]
                : (IBrush?)Application.Current!.Resources["TextSecondaryBrush"]
        };

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { nameText }
        };

        // Pin icon for pinned channels
        if (channel.IsPinned)
        {
            var pinIcon = IconFactory.Create(
                "Icon.Pin",
                (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                12);
            pinIcon.Margin = new Thickness(4, 0, 0, 0);
            contentStack.Children.Add(pinIcon);
        }

        var row = new Border
        {
            Padding = new Thickness(12, 6, 12, 6),
            Child = contentStack,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        // Active channel highlight: tinted background behind readable content
        if (channel.IsActive)
        {
            row.Background = new SolidColorBrush(Color.FromArgb(0x15, 0x06, 0x6A, 0xDB));
            row.CornerRadius = new CornerRadius(4);
        }

        // Hover effect
        row.PointerEntered += (_, _) =>
        {
            if (!channel.IsActive)
            {
                row.Background = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
                row.CornerRadius = new CornerRadius(4);
            }
        };
        row.PointerExited += (_, _) =>
        {
            if (!channel.IsActive)
            {
                row.Background = null;
                row.CornerRadius = new CornerRadius(0);
            }
        };

        // Click handler
        row.PointerPressed += (_, _) =>
        {
            _onChannelSelected?.Invoke(channel.Id);
        };

        return row;
    }
}
