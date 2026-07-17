using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

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
        var header = TextStyles.Header("Channels");
        header.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs);

        _channelList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingNone
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
        var nameText = TextStyles.Body($"#{channel.Name}");
        nameText.VerticalAlignment = VerticalAlignment.Center;
        nameText.Foreground = channel.IsActive
            ? (IBrush?)Application.Current!.Resources["TextPrimaryBrush"]
            : (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
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
            pinIcon.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);
            contentStack.Children.Add(pinIcon);
        }

        var row = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs),
            Child = contentStack,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        // Active channel highlight: tinted background behind readable content
        if (channel.IsActive)
        {
            row.Background = new SolidColorBrush(Color.FromArgb(0x15, 0x06, 0x6A, 0xDB));
            row.CornerRadius = LayoutTokens.RadiusSm;
        }

        // Hover effect
        row.PointerEntered += (_, _) =>
        {
            if (!channel.IsActive)
            {
                row.Background = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
                row.CornerRadius = LayoutTokens.RadiusSm;
            }
        };
        row.PointerExited += (_, _) =>
        {
            if (!channel.IsActive)
            {
                row.Background = null;
                row.CornerRadius = LayoutTokens.NoneRadius;
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
