using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Models;

namespace Zaide.Views;

public class TownhallChannelPanel : UserControl
{
    private readonly StackPanel _listPanel;

    public static readonly StyledProperty<System.Collections.Generic.IReadOnlyList<Channel>?> ChannelsProperty =
        AvaloniaProperty.Register<TownhallChannelPanel, System.Collections.Generic.IReadOnlyList<Channel>?>(nameof(Channels));

    public System.Collections.Generic.IReadOnlyList<Channel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public event Action<string>? ChannelSelected;

    public TownhallChannelPanel()
    {
        // Section header
        var header = new TextBlock
        {
            Text = "CHANNELS",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            Margin = new Thickness(12, 10, 12, 8)
        };

        _listPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            Margin = new Thickness(6, 0, 6, 6)
        };

        var separator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SurfaceBorder"]
        };

        Background = (IBrush?)Application.Current!.Resources["GlassBase"];

        Content = new DockPanel
        {
            Children =
            {
                new Border { Child = header, [DockPanel.DockProperty] = Dock.Top },
                separator,
                _listPanel
            }
        };

        this.GetObservable(ChannelsProperty).Subscribe(_ => RenderChannels());
    }

    private void RenderChannels()
    {
        _listPanel.Children.Clear();
        if (Channels is null)
            return;

        foreach (var channel in Channels)
        {
            var isActive = channel.IsActive;

            var hashPrefix = new TextBlock
            {
                Text = "# ",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = isActive
                    ? (IBrush?)Application.Current!.Resources["TextActive"]
                    : (IBrush?)Application.Current!.Resources["TextSecondary"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var channelName = new TextBlock
            {
                Text = channel.Name,
                FontSize = 12,
                Foreground = isActive
                    ? (IBrush?)Application.Current!.Resources["TextActive"]
                    : (IBrush?)Application.Current!.Resources["TextSecondary"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                Margin = new Thickness(6, 1),
                Children = { hashPrefix, channelName }
            };

            var border = new Border
            {
                Height = 28,
                Padding = new Thickness(6, 0),
                CornerRadius = new CornerRadius(5),
                Background = isActive
                    ? (IBrush?)Application.Current!.Resources["ActiveHighlight"]
                    : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = row
            };

            var channelId = channel.Id;
            border.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                    return;

                ChannelSelected?.Invoke(channelId);
                e.Handled = true;
            };

            // Hover effect
            border.PointerEntered += (_, _) =>
            {
                if (!isActive)
                    border.Background = (IBrush?)Application.Current!.Resources["HoverSurface"];
            };
            border.PointerExited += (_, _) =>
            {
                if (!isActive)
                    border.Background = Brushes.Transparent;
            };

            _listPanel.Children.Add(border);
        }
    }
}